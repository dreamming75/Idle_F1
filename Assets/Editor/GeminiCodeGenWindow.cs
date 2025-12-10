using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class GeminiCodeGenWindow : EditorWindow
{
    // ✅ 테스트는 여기 넣어도 되지만, 깃에 커밋하면 큰일남
    // 가능하면 EditorPrefs나 환경변수로 빼는 게 좋음
    private string apiKey = "";
    private string model = "gemini-2.5-flash";

    private string userRequest =
@"유니티에서 플레이어 이동 스크립트 만들어줘.
- Rigidbody 사용
- WASD 이동
- 점프(스페이스)
- public float moveSpeed, jumpForce";

    private string outputFolder = "Assets/Generated";
    private string fileName = "PlayerMovement.cs";

    private string status = "";
    private Vector2 scroll;

    [MenuItem("Tools/Gemini/Code Generator")]
    public static void Open()
    {
        GetWindow<GeminiCodeGenWindow>("Gemini CodeGen");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Gemini Settings", EditorStyles.boldLabel);

        apiKey = EditorGUILayout.PasswordField("API Key", apiKey);
        model = EditorGUILayout.TextField("Model", model);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Generate Target", EditorStyles.boldLabel);

        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        fileName = EditorGUILayout.TextField("File Name", fileName);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Request (요구사항)", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
        userRequest = EditorGUILayout.TextArea(userRequest, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(apiKey)))
        {
            if (GUILayout.Button("Generate & Save .cs"))
            {
                _ = GenerateAndSaveAsync();
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(status, MessageType.Info);
    }

    private async Task GenerateAndSaveAsync()
    {
        try
        {
            status = "요청 보내는 중...";
            Repaint();

            // “유니티 코드 생성”에 최적화된 프롬프트 템플릿
            var prompt = BuildPrompt(userRequest);

            var raw = await CallGeminiAsync(prompt);
            if (string.IsNullOrEmpty(raw))
            {
                status = "응답이 비었음. 콘솔 확인해봐라.";
                return;
            }

            var code = ExtractCSharpCode(raw);
            if (string.IsNullOrEmpty(code))
            {
                status = "C# 코드 블록을 못 찾겠음. raw를 콘솔로 확인해봐라.";
                Debug.Log(raw);
                return;
            }

            // 저장 폴더 생성
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var path = Path.Combine(outputFolder, fileName);
            File.WriteAllText(path, code, Encoding.UTF8);

            AssetDatabase.Refresh();

            status = $"저장 완료: {path}\n컴파일 에러 나면 요구사항을 더 구체적으로 적어라.";
        }
        catch (Exception e)
        {
            status = "에러 발생: " + e.Message;
            Debug.LogException(e);
        }
        finally
        {
            Repaint();
        }
    }

    private static string BuildPrompt(string request)
    {
        // 모델이 “설명” 말고 “코드만” 주도록 강하게 유도하는 게 포인트임다.
        return
$@"너는 Unity C# 전문가다.
아래 요구사항을 만족하는 **단일 C# 스크립트 파일**을 만들어라.

규칙:
- 반드시 ```csharp 코드블록``` 하나로만 출력하라 (다른 설명 금지)
- Unity 2021+ 호환 코드로 작성하라
- 네임스페이스는 넣지 말고, 필요한 using만 넣어라
- 컴파일 에러 나면 안 된다
- public 필드는 Inspector에서 조절 가능하게 하라
- 요구사항에 없는 외부 패키지 의존성 넣지 마라

요구사항:
{request}
";
    }

    private async Task<string> CallGeminiAsync(string prompt)
    {
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        // JSON 바디 최소 형태
        var json =
            "{\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":" + EscapeJson(prompt) + "}]}]}";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Gemini error: {req.responseCode}\n{req.error}\n{req.downloadHandler.text}");
            return null;
        }

        return req.downloadHandler.text;
    }

    private static string EscapeJson(string s)
    {
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }

    /// <summary>
    /// Gemini 응답(raw JSON)에서 ```csharp ...``` 코드블록만 뽑는 “대충 파서”.
    /// JSON 파싱(정석) 없이도 일단은 잘 굴러가게 만든 버전임다.
    /// </summary>
    private static string ExtractCSharpCode(string raw)
    {
        // raw 안에 코드블록이 그대로 들어오는 경우가 많아서 정규식으로 뽑음
        var m = Regex.Match(raw, "```csharp\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase);
        if (!m.Success) m = Regex.Match(raw, "```\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var code = m.Groups[1].Value.Trim();

        // 혹시 BOM/이상한 문자 섞이면 제거
        code = code.Replace("\r\n", "\n").Replace("\r", "\n");
        return code;
    }
}
