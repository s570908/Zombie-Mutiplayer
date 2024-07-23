using Photon.Pun;
using UnityEngine;

/*****************

새로운 Playerinput 스크립트는 자신의 게임 오브젝트가 로컬 플레이어인 경우에만 사용자 입력을 감지하고, 
그렇지 않은 경우 사용자 입력을 무시하도록 재구성되었습니다.
멀티플레이어 게임에는 씬에 둘 이상의 플레이어 캐릭터가 존재합니다. 
그런데 Playerinput 스크립트는 Player Character 게임 오브젝트가 로컬 플레이어든 리모트 플레이어든 
무조건 추가되어 있습니다.

이때 기존 Playerinput 스크립트를 사용하면 내 것이 아닌 다른 사람의 플레이어 캐릭터도 
조종할수 있는 문제가 생깁니다.

새로운 Playerinput 스크립트는 기존 Playerinput 스크립트에서 단 두 부분만 변경되었습니다.
    • MonoBehaviour 대신 MonoBehaviourPun 사용 
    • Update() 메서드 상단에 새로운 if 문 추가

MonoBehaviourPun은 photonView 프로퍼티를 통해 게임 오브젝트의 Photon View 컴포넌트에 
즉시 접근하기 위해 사용했습니다.

두 번째 변경 사항인 Update() 메서드의 최상단에 새로 추가된 if 문 블록을 주목합니다.
if (IphotonView.IsMine)
{
}
photonView.IsMine은 현재 게임 오브젝트가 로컬 게임 오브젝트인 경우에만 true가 됩니다. 
즉，어떤 Player Character 게임 오브젝트가 리모트 플레이어인 경우 
!photonView.IsMine은 true가 되어 위 if 문의 조건을 만족하고 즉시 return;이 실행됩니다. 
따라서 Update() 메서드 하단의 입력 감지 부분에 처리가 도달하지 못합니다.

******************/

// 플레이어 캐릭터를 조작하기 위한 사용자 입력을 감지
// 감지된 입력값을 다른 컴포넌트들이 사용할 수 있도록 제공
public class PlayerInput : MonoBehaviourPun
{
    public string moveAxisName = "Vertical"; // 앞뒤 움직임을 위한 입력축 이름
    public string rotateAxisName = "Horizontal"; // 좌우 회전을 위한 입력축 이름
    public string fireButtonName = "Fire1"; // 발사를 위한 입력 버튼 이름
    public string reloadButtonName = "Reload"; // 재장전을 위한 입력 버튼 이름

    // 값 할당은 내부에서만 가능
    public float move { get; private set; } // 감지된 움직임 입력값
    public float rotate { get; private set; } // 감지된 회전 입력값
    public bool fire { get; private set; } // 감지된 발사 입력값
    public bool reload { get; private set; } // 감지된 재장전 입력값

    // 매프레임 사용자 입력을 감지
    private void Update()
    {
        // 로컬 플레이어가 아닌 경우 입력을 받지 않음
        if (!photonView.IsMine)
        {
            return;
        }

        // 게임오버 상태에서는 사용자 입력을 감지하지 않는다
        if (GameManager.instance != null
            && GameManager.instance.isGameover)
        {
            move = 0;
            rotate = 0;
            fire = false;
            reload = false;
            return;
        }

        // move에 관한 입력 감지
        move = Input.GetAxis(moveAxisName);
        // rotate에 관한 입력 감지
        rotate = Input.GetAxis(rotateAxisName);
        // fire에 관한 입력 감지
        fire = Input.GetButton(fireButtonName);
        // reload에 관한 입력 감지
        reload = Input.GetButtonDown(reloadButtonName);
    }
}