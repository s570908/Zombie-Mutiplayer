﻿using Photon.Pun;
using UnityEngine;

/* 

기존 PlayerShooter 스크립트는 PlayerInput을 통해 전달받은 입력값으로 총 발사를 시도하고 탄알 UI를 갱신합니다.
변경된 PlayerShooter 스크립트는 자신의 게임 오브젝트가 로컬 플레이어인 경우에만 총 발사를 시도하고 
탄알 UI를 갱신합니다.

로컬 플레이어 입장에서는 네트워크 너머의 다른 사람이 조작하는 리모트 플레이어 캐릭터의 남은 탄알이 
UI로 표시될 필요가 없습니다. 따라서 PlayerShooter 스크립트가 붙어 있는 게임 오브젝트가 
로컬 플레이어 캐릭터일 때만 탄알 UI를 갱신합니다.
리모트 플레이어 캐릭터에 붙어 있는 PlayerShooter 스크립트는 입력을 감지하는 부분이 동작 하지 않아야 합니다. 
동기화를 통해 로컬의 총 발사 처리에 따라서 리모트의 총 발사가 자동으로 이루어질 것이기 때문입니다. 
총 발사 처리의 동기화는 19.3절 ‘네트워크 Gun’에서 구현합니다.

결론적으로 리모트 플레이어 입장에서는 조작을 감지하고 다른 메서드를 직접 실행하는 부분이 동작하지 않도록 막아야 합니다. 

*/

// 주어진 Gun 오브젝트를 쏘거나 재장전
// 알맞은 애니메이션을 재생하고 IK를 사용해 캐릭터 양손이 총에 위치하도록 조정
public class PlayerShooter : MonoBehaviourPun
{
    public Gun gun; // 사용할 총
    public Transform gunPivot; // 총 배치의 기준점
    public Transform leftHandMount; // 총의 왼쪽 손잡이, 왼손이 위치할 지점
    public Transform rightHandMount; // 총의 오른쪽 손잡이, 오른손이 위치할 지점

    private PlayerInput playerInput; // 플레이어의 입력
    private Animator playerAnimator; // 애니메이터 컴포넌트

    private void Start()
    {
        // 사용할 컴포넌트들을 가져오기
        playerInput = GetComponent<PlayerInput>();
        playerAnimator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        // 슈터가 활성화될 때 총도 함께 활성화
        gun.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        // 슈터가 비활성화될 때 총도 함께 비활성화
        gun.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 로컬 플레이어만 총을 직접 사격, 탄약 UI 갱신 가능
        if (!photonView.IsMine)
        {
            return;
        }

        // 입력을 감지하고 총 발사하거나 재장전
        if (playerInput.fire)
        {
            // 발사 입력 감지시 총 발사
            gun.Fire();
        }
        else if (playerInput.reload)
        {
            // 재장전 입력 감지시 재장전
            if (gun.Reload())
            {
                // 재장전 성공시에만 재장전 애니메이션 재생
                playerAnimator.SetTrigger("Reload");
            }
        }

        // 남은 탄약 UI를 갱신
        UpdateUI();
    }

    // 탄약 UI 갱신
    private void UpdateUI()
    {
        if (gun != null && UIManager.instance != null)
        {
            // UI 매니저의 탄약 텍스트에 탄창의 탄약과 남은 전체 탄약을 표시
            UIManager.instance.UpdateAmmoText(gun.magAmmo, gun.ammoRemain);
        }
    }

    // 애니메이터의 IK 갱신
    private void OnAnimatorIK(int layerIndex)
    {
        // 총의 기준점 gunPivot을 3D 모델의 오른쪽 팔꿈치 위치로 이동
        gunPivot.position = playerAnimator.GetIKHintPosition(AvatarIKHint.RightElbow);

        // IK를 사용하여 왼손의 위치와 회전을 총의 오른쪽 손잡이에 맞춘다
        playerAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1.0f);
        playerAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1.0f);

        playerAnimator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandMount.position);
        playerAnimator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandMount.rotation);

        // IK를 사용하여 오른손의 위치와 회전을 총의 오른쪽 손잡이에 맞춘다
        playerAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1.0f);
        playerAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1.0f);

        playerAnimator.SetIKPosition(AvatarIKGoal.RightHand, rightHandMount.position);
        playerAnimator.SetIKRotation(AvatarIKGoal.RightHand, rightHandMount.rotation);
    }
}