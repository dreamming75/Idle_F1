using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public class ChatGPTCodeGenWindow : EditorWindow
{
    [TextArea(8, 20)]
    private string requirements =
@"유니티 C# 스크립트 생성:
- 기능: 플레이어 이동
- 입력: WASD, 점프 Space
- Rigidbody 기반
- public float moveSpeed, jumpForce
- 바닥 체크 포함
- 컴파일 에러 없어야 함";

    private bool includeStrictTemplate = true;

    private string outputFolder = "Assets/Generated";
    private string fileName = "NewScript.cs";

    private bool preventOverwrite = false;
    private bool autoNameFromMainClass = true;
    private bool warnOnFileClassMismatch = true;

    // ✅ 추가: 파일명!=클래스명이면 파일명을 클래스명으로 자동 변경 저장(옵션)
    private bool autoRenameFileToClassName = false;

    [TextArea(12, 30)]
    private string pastedResponse = "";

    private string status = "";
    private Vector2 scrollReq;
    private Vector2 scrollResp;

    private static List<string> s_lastSavedAssetPaths = new();
    private static bool s_openOnCompileErrorPending = false;

    [MenuItem("Tools/ChatGPT/Code Generator (Semi-Auto)")]
    public static void Open()
    {
        GetWindow<ChatGPTCodeGenWindow>("ChatGPT CodeGen");
    }

    private void OnEnable()
    {
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
    }

    private void OnDisable()
    {
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
    }

    private void OnGUI()
    {
        var safeFileName = EnsureCsExtension(fileName);
        var className = DeriveClassNameFromFileName(safeFileName);

        EditorGUILayout.LabelField("설정 미리보기", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("메인 파일명", safeFileName);
        EditorGUILayout.LabelField("메인 클래스명(자동)", className);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("1) 요구사항 작성", EditorStyles.boldLabel);
        scrollReq = EditorGUILayout.BeginScrollView(scrollReq, GUILayout.Height(150));
        requirements = EditorGUILayout.TextArea(requirements, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        includeStrictTemplate = EditorGUILayout.ToggleLeft(
            "ChatGPT에 '코드만 출력' 강제 템플릿 포함",
            includeStrictTemplate);

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("저장 설정", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        fileName = EditorGUILayout.TextField("File Name", fileName);

        preventOverwrite = EditorGUILayout.ToggleLeft("기존 파일 덮어쓰기 금지(옵션)", preventOverwrite);
        autoNameFromMainClass = EditorGUILayout.ToggleLeft("파일 힌트 없으면 class명으로 파일명 자동 지정(옵션)", autoNameFromMainClass);
        warnOnFileClassMismatch = EditorGUILayout.ToggleLeft("파일명=클래스명 불일치 경고(옵션)", warnOnFileClassMismatch);

        using (new EditorGUI.DisabledScope(!warnOnFileClassMismatch))
        {
            autoRenameFileToClassName = EditorGUILayout.ToggleLeft("불일치면 파일명을 클래스명으로 자동 변경 저장(옵션)", autoRenameFileToClassName);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("프롬프트 생성 → 클립보드 복사", GUILayout.Height(28)))
        {
            var prompt = BuildPrompt(requirements, safeFileName, includeStrictTemplate);
            EditorGUIUtility.systemCopyBuffer = prompt;
            status = "프롬프트 클립보드 복사했데이. 브라우저 ChatGPT에 그대로 붙여넣어라.";
        }
        if (GUILayout.Button("ChatGPT 열기(브라우저)", GUILayout.Height(28)))
        {
            Application.OpenURL("https://chatgpt.com/");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("2) ChatGPT 응답(코드) 붙여넣기", EditorStyles.boldLabel);
        scrollResp = EditorGUILayout.BeginScrollView(scrollResp, GUILayout.Height(220));
        pastedResponse = EditorGUILayout.TextArea(pastedResponse, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("코드 분리/추출 미리보기(콘솔)", GUILayout.Height(26)))
        {
            PreviewExtraction(safeFileName);
        }

        if (GUILayout.Button("분리 저장(.cs) + 백업 + Refresh", GUILayout.Height(26)))
        {
            SaveAllExtractedFiles(safeFileName);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(status, MessageType.Info);
    }

    // -----------------------------
    // Prompt
    // -----------------------------
    private static string BuildPrompt(string req, string targetFileName, bool strict)
    {
        var safeFileName = EnsureCsExtension(targetFileName);
        var className = DeriveClassNameFromFileName(safeFileName);

        if (!strict) return req;

        return
$@"너는 Unity C# 전문가다. 아래 요구사항을 만족하는 **C# 스크립트 파일(들)**을 작성하라.

중요 규칙:
- 출력은 반드시 ```csharp``` 코드블록만 사용하라 (설명/다른 텍스트 금지)
- 여러 파일이 필요하면, 각 코드블록 맨 첫 줄에 반드시 파일명을 써라:
  예) // File: PlayerMovement.cs
- 첫 번째(메인) 파일명은 {safeFileName} 이며, 메인 클래스명은 {className} 이어야 한다(파일명=클래스명).
- Unity 2021+ 호환
- 컴파일 에러 없어야 함
- 외부 패키지 의존성 추가 금지
- public 필드는 Inspector에서 조절 가능하게 하라
- 필요 시 [RequireComponent] 사용

요구사항:
{req}";
    }

    private static string EnsureCsExtension(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "NewScript.cs";
        return name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? name : name + ".cs";
    }

    private static string DeriveClassNameFromFileName(string csFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(csFileName).Trim();
        if (string.IsNullOrEmpty(baseName)) return "NewScript";

        baseName = Regex.Replace(baseName, @"[^a-zA-Z0-9_]", "_");
        if (char.IsDigit(baseName[0])) baseName = "_" + baseName;

        return baseName;
    }

    // -----------------------------
    // Preview
    // -----------------------------
    private void PreviewExtraction(string mainFileName)
    {
        var extracted = ExtractFilesFromResponse(pastedResponse, mainFileName, autoNameFromMainClass);

        if (extracted.Count == 0)
        {
            status = "코드블록을 못 찾겠데이. 응답을 통째로 붙였는지 확인해봐라.";
            return;
        }

        status = $"추출 성공: {extracted.Count}개 파일 후보. 콘솔에 제목/앞부분 찍어놨데이.";
        Debug.Log($"[ChatGPTCodeGen] Extracted {extracted.Count} file(s):");

        foreach (var kv in extracted)
        {
            var preview = kv.Value;
            if (preview.Length > 400) preview = preview.Substring(0, 400) + "\n... (truncated)";
            Debug.Log($"--- {kv.Key} ---\n{preview}");
        }
    }

    // -----------------------------
    // Save
    // -----------------------------
    private void SaveAllExtractedFiles(string mainFileName)
    {
        var extracted = ExtractFilesFromResponse(pastedResponse, mainFileName, autoNameFromMainClass);
        if (extracted.Count == 0)
        {
            status = "저장 실패: 코드블록을 못 찾겠데이. ChatGPT 응답(코드블록 포함)을 통째로 붙여넣어라.";
            return;
        }

        try
        {
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var savedAssetPaths = new List<string>();
            var skipped = new List<string>();
            var mismatchWarnings = new List<string>();
            var renamed = new List<string>();

            // 이미 이번 세션에서 저장하려는 파일명들(중복 방지)
            var plannedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in extracted)
            {
                var code = kv.Value;

                // 1) 기본 파일명 후보
                var outName = EnsureCsExtension(kv.Key);
                outName = SanitizeFileName(outName);

                // 2) 파일명/클래스명 불일치 검사 + 자동 리네임(옵션)
                string expectedClass = DeriveClassNameFromFileName(outName);
                string actualClass = TryFindMainClassName(code);

                if (warnOnFileClassMismatch && !string.IsNullOrEmpty(actualClass) &&
                    !string.Equals(expectedClass, actualClass, StringComparison.Ordinal))
                {
                    var warnText = $"파일명({expectedClass}) != 클래스명({actualClass})";
                    mismatchWarnings.Add($"{outName}: {warnText}");

                    // 클릭 가능 경고 로그 (라인 1)
                    Debug.LogWarning($"{ToAssetPath(Path.Combine(outputFolder, outName)) ?? outName}(1,1): warning: {warnText}");

                    // ✅ 자동 리네임
                    if (autoRenameFileToClassName)
                    {
                        var renamedFile = SanitizeFileName(actualClass + ".cs");

                        // 이번 저장 세션 내부 충돌 처리
                        renamedFile = MakeUniqueFileName(renamedFile, plannedNames);

                        renamed.Add($"{outName} -> {renamedFile}");
                        outName = renamedFile;

                        // 리네임 후 expectedClass 갱신
                        expectedClass = DeriveClassNameFromFileName(outName);
                    }
                }

                plannedNames.Add(outName);

                var fullPath = Path.Combine(outputFolder, outName);

                // 덮어쓰기 금지면 스킵
                if (preventOverwrite && File.Exists(fullPath))
                {
                    skipped.Add(fullPath);
                    continue;
                }

                BackupIfExists(fullPath);
                File.WriteAllText(fullPath, code);

                var assetPath = ToAssetPath(fullPath);
                if (!string.IsNullOrEmpty(assetPath))
                    savedAssetPaths.Add(assetPath);
            }

            AssetDatabase.Refresh();

            s_lastSavedAssetPaths = savedAssetPaths;
            s_openOnCompileErrorPending = savedAssetPaths.Count > 0;

            var msg = new List<string>();
            if (savedAssetPaths.Count > 0)
                msg.Add($"저장 완료: {savedAssetPaths.Count}개\n- " + string.Join("\n- ", savedAssetPaths));
            if (renamed.Count > 0)
                msg.Add($"(자동 리네임 적용: {renamed.Count}개)\n- " + string.Join("\n- ", renamed));
            if (skipped.Count > 0)
                msg.Add($"(덮어쓰기 금지로 스킵: {skipped.Count}개)\n- " + string.Join("\n- ", skipped.Select(ToAssetPathOrRaw)));
            if (mismatchWarnings.Count > 0 && !autoRenameFileToClassName)
                msg.Add($"(경고) 파일명/클래스명 불일치: {mismatchWarnings.Count}개\n- " + string.Join("\n- ", mismatchWarnings));

            status = string.Join("\n\n", msg);
        }
        catch (Exception e)
        {
            status = "저장 중 에러: " + e.Message;
            Debug.LogException(e);
        }
    }

    private static string SanitizeFileName(string file)
    {
        file = Path.GetFileName(file);
        file = Regex.Replace(file, @"[^a-zA-Z0-9_\.\-]", "_");
        if (string.IsNullOrWhiteSpace(file)) file = "Generated.cs";
        if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) file += ".cs";
        return file;
    }

    private static void BackupIfExists(string path)
    {
        if (!File.Exists(path)) return;

        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var bakName = $"{name}.bak_{stamp}{ext}";
        var bakPath = Path.Combine(dir ?? "", bakName);

        File.Copy(path, bakPath, overwrite: true);
    }

    private static string ToAssetPath(string fullPath)
    {
        fullPath = fullPath.Replace("\\", "/");
        var dataPath = Application.dataPath.Replace("\\", "/");
        if (!fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return null;
        return "Assets" + fullPath.Substring(dataPath.Length);
    }

    private static string ToAssetPathOrRaw(string fullPath) => ToAssetPath(fullPath) ?? fullPath;

    // -----------------------------
    // Auto-open + line jump on compile errors
    // -----------------------------
    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        if (!s_openOnCompileErrorPending) return;

        var errors = messages?.Where(m => m.type == CompilerMessageType.Error).ToList() ?? new List<CompilerMessage>();
        if (errors.Count == 0)
        {
            s_openOnCompileErrorPending = false;
            return;
        }

        CompilerMessage? firstRelevant = null;

        foreach (var e in errors)
        {
            if (string.IsNullOrEmpty(e.file)) continue;
            var assetPath = AbsoluteToAssetPath(e.file);
            if (!string.IsNullOrEmpty(assetPath) &&
                s_lastSavedAssetPaths.Any(p => string.Equals(p, assetPath, StringComparison.OrdinalIgnoreCase)))
            {
                firstRelevant = e;
                break;
            }
        }

        var fallbackAsset = s_lastSavedAssetPaths.FirstOrDefault();

        if (firstRelevant.HasValue)
        {
            var e = firstRelevant.Value;
            var assetPath = AbsoluteToAssetPath(e.file);
            OpenAsset(assetPath);

            int line = Math.Max(1, e.line);
            int col = Math.Max(1, e.column);
            Debug.LogError($"{assetPath}({line},{col}): error: {e.message}");
        }
        else if (!string.IsNullOrEmpty(fallbackAsset))
        {
            OpenAsset(fallbackAsset);
            var e = errors[0];
            Debug.LogError($"{fallbackAsset}(1,1): error: 컴파일 에러 감지됨 (첫 에러: {e.message})");
        }

        s_openOnCompileErrorPending = false;
    }

    private static void OpenAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return;
        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        if (obj != null) AssetDatabase.OpenAsset(obj);
    }

    private static string AbsoluteToAssetPath(string absolutePath)
    {
        absolutePath = (absolutePath ?? "").Replace("\\", "/");
        var dataPath = Application.dataPath.Replace("\\", "/");
        if (!absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return null;
        return "Assets" + absolutePath.Substring(dataPath.Length);
    }

    // -----------------------------
    // Extraction: multiple files support (+ class-name auto file-name)
    // -----------------------------
    private static Dictionary<string, string> ExtractFilesFromResponse(string response, string defaultMainFileName, bool autoNameFromClass)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(response)) return result;

        var blocks = new List<string>();

        foreach (Match m in Regex.Matches(response, "```csharp\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase))
            blocks.Add(m.Groups[1].Value);

        if (blocks.Count == 0)
        {
            foreach (Match m in Regex.Matches(response, "```\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase))
                blocks.Add(m.Groups[1].Value);
        }

        if (blocks.Count == 0 && (response.Contains("class ") || response.Contains("using ")))
            blocks.Add(response);

        if (blocks.Count == 0) return result;

        int unnamedIndex = 1;

        foreach (var rawBlock in blocks)
        {
            var code = Clean(rawBlock);

            if (TryExtractFileNameHint(code, out var hintedName) && !string.IsNullOrWhiteSpace(hintedName))
            {
                code = RemoveFirstLine(code);
                hintedName = EnsureCsExtension(hintedName);
                result[hintedName] = code;
                continue;
            }

            if (autoNameFromClass)
            {
                var className = TryFindMainClassName(code);
                if (!string.IsNullOrEmpty(className))
                {
                    var autoFile = SanitizeFileName(className + ".cs");
                    autoFile = MakeUniqueFileName(autoFile, result.Keys);
                    result[autoFile] = code;
                    continue;
                }
            }

            if (!result.ContainsKey(defaultMainFileName))
            {
                result[defaultMainFileName] = code;
            }
            else
            {
                var gen = $"Generated_{unnamedIndex:00}.cs";
                unnamedIndex++;
                result[gen] = code;
            }
        }

        return result;
    }

    private static string MakeUniqueFileName(string fileName, IEnumerable<string> existingKeysOrPlanned)
    {
        if (!existingKeysOrPlanned.Any(k => string.Equals(k, fileName, StringComparison.OrdinalIgnoreCase)))
            return fileName;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);

        for (int i = 2; i < 999; i++)
        {
            var candidate = $"{baseName}_{i}{ext}";
            if (!existingKeysOrPlanned.Any(k => string.Equals(k, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return fileName;
    }

    private static string TryFindMainClassName(string code)
    {
        var m = Regex.Match(code ?? "", @"\bpublic\s+(partial\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)\b");
        if (m.Success) return m.Groups[2].Value;

        m = Regex.Match(code ?? "", @"\b(partial\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)\b");
        if (m.Success) return m.Groups[2].Value;

        return null;
    }

    private static bool TryExtractFileNameHint(string code, out string fileName)
    {
        fileName = null;
        if (string.IsNullOrWhiteSpace(code)) return false;

        var lines = GetFirstLines(code, 2);

        var patterns = new[]
        {
            @"^\s*//\s*file\s*:\s*(.+?)\s*$",
            @"^\s*//\s*filename\s*:\s*(.+?)\s*$",
            @"^\s*//\s*name\s*:\s*(.+?)\s*$",
            @"^\s*#\s*file\s*:\s*(.+?)\s*$",
        };

        foreach (var line in lines)
        {
            foreach (var p in patterns)
            {
                var m = Regex.Match(line, p, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    fileName = m.Groups[1].Value.Trim().Trim('\"', '\'', '`');
                    return true;
                }
            }
        }

        return false;
    }

    private static string[] GetFirstLines(string text, int count)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        if (lines.Length <= count) return lines;

        var head = new string[count];
        Array.Copy(lines, head, count);
        return head;
    }

    private static string RemoveFirstLine(string text)
    {
        var t = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        var idx = t.IndexOf('\n');
        if (idx < 0) return "";
        return t.Substring(idx + 1).TrimStart('\n');
    }

    private static string Clean(string code)
    {
        code = (code ?? "").Trim();
        code = code.Replace("\r\n", "\n").Replace("\r", "\n");
        code = code.Trim('`').Trim();
        return code;
    }
}
