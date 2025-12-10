using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class GeminiSimpleClient : MonoBehaviour
{
    [Header("Security tip: don't commit this key to git.")]
    [SerializeField] private string apiKey = "YOUR_API_KEY";

    // 텍스트 전용: gemini-1.5-flash (빠르고 싸서 테스트용으로 좋음)
    private const string Model = "gemini-2.5-flash";

    [TextArea(3, 10)]
    public string prompt = "유니티에서 제미나이 연동 테스트 중임. 인사 한번 해줘.";

    [ContextMenu("Send Test Prompt")]
    public void SendTestPrompt()
    {
        _ = SendTextAsync(prompt);
    }

    public async Task<string> SendTextAsync(string userText)
    {
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={apiKey}";

        // Gemini REST 바디 (최소 형태)
        var json = "{\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":" + Escape(userText) + "}]}]}";

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

        var raw = req.downloadHandler.text;
        Debug.Log($"Gemini raw response:\n{raw}");

        // 간단히 텍스트만 뽑고 싶으면 JSON 파싱이 필요하긴 한데,
        // 우선은 raw 찍히는 것만으로도 연동 성공 여부 판단 가능함.
        return raw;
    }

    private static string Escape(string s)
    {
        // JSON string escape 최소 구현
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }
}
