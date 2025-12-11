using UnityEngine;
using System.Reflection;

namespace IdleF1.Combat
{
    /// <summary>
    /// CinemachineCamera가 Player를 따라가도록 설정하는 컴포넌트
    /// </summary>
    public class CinemachinePlayerFollow : MonoBehaviour
    {
        [Header("타겟 설정")]
        [SerializeField]
        private Transform playerTarget;
        
        [SerializeField]
        private string playerTag = "Player";
        
        [Header("카메라 설정")]
        [SerializeField]
        private bool activateOnStart = true;
        
        [SerializeField]
        private Vector3 followOffset = new Vector3(0, 0, -10);
        
        private Component cinemachineCamera;
        private Component cinemachineFollow;

        private void Awake()
        {
            // 리플렉션을 사용하여 Cinemachine 컴포넌트 가져오기
            System.Type cinemachineCameraType = System.Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine");
            System.Type cinemachineFollowType = System.Type.GetType("Unity.Cinemachine.CinemachineFollow, Unity.Cinemachine");
            
            if (cinemachineCameraType != null)
            {
                cinemachineCamera = GetComponent(cinemachineCameraType);
            }
            
            if (cinemachineFollowType != null)
            {
                cinemachineFollow = GetComponent(cinemachineFollowType);
            }
            
            // Player를 자동으로 찾기
            if (playerTarget == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObj != null)
                {
                    playerTarget = playerObj.transform;
                }
            }
        }

        private void Start()
        {
            SetupCamera();
            
            if (activateOnStart)
            {
                gameObject.SetActive(true);
            }
        }

        private void SetupCamera()
        {
            if (cinemachineCamera == null || playerTarget == null)
            {
                Debug.LogWarning("CinemachinePlayerFollow: CinemachineCamera 또는 Player 타겟이 설정되지 않았습니다!");
                return;
            }
            
            // 리플렉션을 사용하여 TrackingTarget 설정
            PropertyInfo trackingTargetProp = cinemachineCamera.GetType().GetProperty("TrackingTarget");
            if (trackingTargetProp != null)
            {
                trackingTargetProp.SetValue(cinemachineCamera, playerTarget);
            }
            
            // CinemachineFollow의 FollowOffset 설정
            if (cinemachineFollow != null)
            {
                PropertyInfo followOffsetProp = cinemachineFollow.GetType().GetProperty("FollowOffset");
                if (followOffsetProp != null)
                {
                    followOffsetProp.SetValue(cinemachineFollow, followOffset);
                }
            }
        }

        /// <summary>
        /// Player 타겟을 동적으로 설정
        /// </summary>
        public void SetPlayerTarget(Transform target)
        {
            playerTarget = target;
            SetupCamera();
        }

        /// <summary>
        /// Follow Offset 설정
        /// </summary>
        public void SetFollowOffset(Vector3 offset)
        {
            followOffset = offset;
            if (cinemachineFollow != null)
            {
                PropertyInfo followOffsetProp = cinemachineFollow.GetType().GetProperty("FollowOffset");
                if (followOffsetProp != null)
                {
                    followOffsetProp.SetValue(cinemachineFollow, offset);
                }
            }
        }
    }
}

