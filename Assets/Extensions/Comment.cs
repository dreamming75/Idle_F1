using UnityEngine;
using UnityEngine.Serialization;

#pragma warning disable 0414        
namespace H.Common
{
    /// <summary>
    /// 단순히 주석용으로 사용
    /// </summary>
    public class Comment : MonoBehaviour
    {
        [SerializeField, Multiline, FormerlySerializedAs("_comment")]
        private string comment = default;
    }
}
#pragma warning restore 0414