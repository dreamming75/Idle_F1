using UnityEngine;
using System.Collections;

public class HideAnimation : MonoBehaviour
{
    [Header("필수 컴포넌트")]
    public Animation targetAnimation;

    [Header("애니메이션 클립")]
    public AnimationClip showClip; // 켜질 때
    public AnimationClip hideClip; // 꺼질 때

    private void OnEnable()
    {
        if (targetAnimation != null && showClip != null)
        {
            string clipName = showClip.name;

            if (!targetAnimation.GetClip(clipName))
            {
                targetAnimation.AddClip(showClip, clipName);
            }

            targetAnimation.Play(clipName);
        }
    }

    public void HideWithAnimation()
    {
        if (targetAnimation == null || hideClip == null)
        {
            Debug.LogWarning("Animation 또는 HideClip이 지정되지 않았습니다.");
            return;
        }

        string clipName = hideClip.name;

        if (!targetAnimation.GetClip(clipName))
        {
            targetAnimation.AddClip(hideClip, clipName);
        }

        targetAnimation.Play(clipName);
        StartCoroutine(DisableAfterAnimation(clipName));
    }

    private IEnumerator DisableAfterAnimation(string clipName)
    {
        while (targetAnimation.IsPlaying(clipName))
        {
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
