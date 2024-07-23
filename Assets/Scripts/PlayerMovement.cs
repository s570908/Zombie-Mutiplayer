using Photon.Pun;
using UnityEngine;

/* 

네트워크 게임에서 리모트 플레이어 캐릭터는 네트워크 너머의 로컬 플레이어 캐릭터로부터 위치, 회전, 애니메이터 
파라미터값을 받아 자신의 값으로 사용합니다.
따라서 기존 PlayerMovement 스크립트의 이동，회전，애니메이션 처리는 로컬 플레이어 캐릭터인 경우에만 
실행되도록 재구성해야 합니다. 

리모트 플레이어 캐릭터는 동기화를 통해 위치，회전，애니메이션 파라미터를 로컬 플레이어 캐릭터로부터 
자동으로 받아 사용하기 때문입니다. 

새로운 PlayerMovement 스크립트의 주요 변경 사항은 다음과 같습니다.
    • MonoBehaviour 대신 MonoBehaviourPun 사용
    • FixedUpdate() 메서드 상단에 로컬 여부를 검사하는 if 문 추가

PlayerMovement 스크립트는 FixedUpdate () 메서드의 최상단에 다음과 같은 if 문을 추가하여 
현재 게임 요브젝트가 로컬 게임 오브젝트인 경우에만 이동，회전，애니메이션 파라미터 갱신 처리를 
실행하도록 구현했습니다.

*/

// 플레이어 캐릭터를 사용자 입력에 따라 움직이는 스크립트
public class PlayerMovement : MonoBehaviourPun
{
    public float moveSpeed = 5f; // 앞뒤 움직임의 속도
    public float rotateSpeed = 180f; // 좌우 회전 속도

    private Animator playerAnimator; // 플레이어 캐릭터의 애니메이터
    private PlayerInput playerInput; // 플레이어 입력을 알려주는 컴포넌트
    private Rigidbody playerRigidbody; // 플레이어 캐릭터의 리지드바디

    private void Start()
    {
        // 사용할 컴포넌트들의 참조를 가져오기
        playerInput = GetComponent<PlayerInput>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();
    }

    // FixedUpdate는 물리 갱신 주기에 맞춰 실행됨
    private void FixedUpdate()
    {
        // 로컬 플레이어만 직접 위치와 회전을 변경 가능
        if (!photonView.IsMine)
        {
            return;
        }

        // 회전 실행
        Rotate();
        // 움직임 실행
        Move();

        // 입력값에 따라 애니메이터의 Move 파라미터 값을 변경
        playerAnimator.SetFloat("Move", playerInput.move);
    }

    // 입력값에 따라 캐릭터를 앞뒤로 움직임
    private void Move()
    {
        // 상대적으로 이동할 거리 계산
        Vector3 moveDistance =
            playerInput.move * transform.forward * moveSpeed * Time.deltaTime;
        // 리지드바디를 통해 게임 오브젝트 위치 변경
        playerRigidbody.MovePosition(playerRigidbody.position + moveDistance);
    }

    // 입력값에 따라 캐릭터를 좌우로 회전
    private void Rotate()
    {
        // 상대적으로 회전할 수치 계산
        float turn = playerInput.rotate * rotateSpeed * Time.deltaTime;
        // 리지드바디를 통해 게임 오브젝트 회전 변경
        playerRigidbody.rotation =
            playerRigidbody.rotation * Quaternion.Euler(0, turn, 0f);
    }
}