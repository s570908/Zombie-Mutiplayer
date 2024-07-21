using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

// MonoBehaviourPunCallbacks를 상속한 스크립트는 여러 Photon 이벤트를 감지할 수 있습니다. 
// GameManager 스크립트는 OnLeftRoom() 이벤트를 감지하고 해당 메서드를 자동 실행하기 위해 
// MonoBehaviourPunCallbacks를 상속했습니다.

// 먼저 OnLeftRoom() 메서드를 살펴봅시다. 
// OnLeftRoom() 메서드는 로컬 플레이어가 현재 게임 룸을 나갈 때 자동 실행됩니다.

/************************************
OnLeftRoom() 메서드는 룸을 나가는 로컬 클라이언트에서만 실행되고，다른 클라이언트에서는 실행되지 않습니다. 
따라서 SceneManager.LoadScene("Lobby");에 의해 로컬 클라이언트의 씬만 Lobby 씬으로 변경되고，
다른 클라이언트는 여전히 룸에 접속된 상태로 남습니다.

룸을 나간 사용자는 언제든지 다시 Lobby 씬에서 매치메이킹을 사용해 새로운 랜덤 룸을 찾아 게임에 접속할 수 있습니다.
GameManager 스크립트에 새로 추가된 Update() 메서드에서는 키보드의 Esc키 (KeyCode.Escape)를 눌렀을 때 
네트워크 룸 나가기를 실행합니다.

여기서 사용된 PhotonNetwork.LeaveRoom()은 현재 네트워크 룸을 나가는 메서드입니다. 
단，룸을 나가고 네트워크 접속이 종료된다고 해도 그것이 씬을 전환한다는 의미는 아니므로
OnLeftRoom ()을 구현하여 로비 씬으로 돌아간 겁니다.

Photon View 컴포넌트가 추가된 네트워크 게임 오브젝트가 PhotonNetwork.Instantiate()
를 사용해 게임 도중에 생성된 것이 아닌 처음부터 씬에 있었던 게임 오브젝트라면 그 소유권 은 호스트에 있습니다. 
즉，Game Manager 게임 오브젝트는 처음부터 PhotonNetwork.Instantiate()에 의해서 호스트 클라이언트가 생성되면서 
만들어진 씬에 있었던 게임 오브젝트이고, 따라서 호스트 클라이언트에 로컬인 게임 요브젝트입니다.

19.7절 ‘좀비 생성기 포팅’에서 다룰 좀비 생성기는 생성된 좀비의 사망 시에 호스트의 GameManager 컴포넌트에서만 
AddScore() 메서드가 실행되도록 할 겁니다. 즉，다른 클라이언트에서의 GameManager 컴포넌트는 점수 증가 메서드인 
AddScore()가 실행되지 않습니다. 그러려면 호스트의 GameManager 컴포넌트에서 다른 클라이언트의 GameManager 컴포넌트로 
점수를 동기화해야 합니다.

호스트 입장에서 Game Manager 게임 오브젝트는 로컬입니다. 따라서 다음과 같이 IPunObservable 인터페이스를 상속하고 
OnPhotonSerializeView() 메서드를 구현하여 로컬에서 리모트로의 점수 동기화를 구현하면 호스트에서 갱신된 점수가 
다른 클라이언트에도 자동 반영됩니다.

이 과정에서 리모트 GameManager는 네트워크를 통해 점수를 받아오는 시점에서 
UIManager.instance.UpdateScoreText(score);를 실행하여 UI를 갱신하도록 코드를 작성했습니다. 
호스트에서는 AddScore() 메서드가 실행되면서 UIManager.instance.UpdateScoreText(score);에 의해 UI가 갱신됩니다. 
그런데 다른 클라이언트에서는 AddScore() 메서드가 실행되지 못하므로 동기화가 실행되는 시점에 이를 갱신하도록 한 겁니다.
*************************************/

// 점수와 게임 오버 여부, 게임 UI를 관리하는 게임 매니저
public class GameManager : MonoBehaviourPunCallbacks, IPunObservable
{
    // 외부에서 싱글톤 오브젝트를 가져올때 사용할 프로퍼티
    public static GameManager instance
    {
        get
        {
            // 만약 싱글톤 변수에 아직 오브젝트가 할당되지 않았다면
            if (m_instance == null)
            {
                // 씬에서 GameManager 오브젝트를 찾아 할당
                m_instance = FindObjectOfType<GameManager>();
            }

            // 싱글톤 오브젝트를 반환
            return m_instance;
        }
    }

    private static GameManager m_instance; // 싱글톤이 할당될 static 변수

    public GameObject playerPrefab; // 생성할 플레이어 캐릭터 프리팹

    private int score = 0; // 현재 게임 점수
    public bool isGameover { get; private set; } // 게임 오버 상태

    // 주기적으로 자동 실행되는, 동기화 메서드
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 로컬 오브젝트라면 쓰기 부분이 실행됨
        if (stream.IsWriting)
        {
            // 네트워크를 통해 score 값을 보내기
            stream.SendNext(score);
        }
        else
        {
            // 리모트 오브젝트라면 읽기 부분이 실행됨         

            // 네트워크를 통해 score 값 받기
            score = (int)stream.ReceiveNext();
            // 동기화하여 받은 점수를 UI로 표시
            UIManager.instance.UpdateScoreText(score);
        }
    }

    private void Awake()
    {
        // 씬에 싱글톤 오브젝트가 된 다른 GameManager 오브젝트가 있다면
        if (instance != this)
        {
            // 자신을 파괴
            Destroy(gameObject);
        }
    }

    /*************************************************
    Start()에서 로컬 플레이어 캐릭터 생성

    룸에 접속한 클라이언트들은 자신의 분신으로 싸울 플레이어 캐릭터를 생성해야 합니다. 이들은 각각의 클라이언트 입장에서 
    로컬 플레이어 캐릭터가 됩니다.

    이때 GameManager 스크립트의 Start() 메서드와 그 안의 PhotonNetwork.Instantiate() 메 서드는 
    각각의 클라이언트에서 따로 실행된다는 사실에 주목합니다. 
    
    클라이언트 A와 B가 있다고 가정하고，A가 이미 접속한 상태에서 B가 나중에 룸에 접속했다고 가정해봅시다.
    
    1. 클라이언트 B가 룸에 접속 一> B에서 GameManager의 Start() 실행 
    2. PhotonNetwork.Instantiate()에 의해 플레이어 캐릭터 b를 A와 B에 생성

    클라이언트 B에서 GameManager의 Start() 메서드가 실행될 때 A에서는 실행되지 않는다는 사실에 주목합니다. 
    A가 먼저 룸을 만들고 들어가면서 이미 GameManager의 Start() 메서드를 실행한 상태입니다.

    PhotonNetwork.Instantiate() 메서드는 생성된 네트워크 게임 오브젝트의 소유권을 해당 코드를 직접 실행한 
    클라이언트에 줍니다. 또한 PUN은 어떤 클라이언트가 룸에 접속하기 전에 해당 룸에서 PhotonNetwork.Instantiate()를 
    사용해 이미 생성된 네트워크 게임 오브젝트가 있을 때 뒤늦게 들어온 클라이언트에도 해당 네트워크 게임 오브젝트를 
    자동 생성해줍니다.

    위 사실을 종합해서 다시 자세히 풀어쓰면 다음과 같습니다.

    1. 클라이언트 A가 룸을 생성하고 접속
    2. A에서 GameManager의 Start() 실행
    3. A가 PhotonNetwork.Instantiate()에 의해 플레이어 캐릭터 a를 A에 생성
    4. 클라이언트 B가 룸에 접속 一> 클라이언트 B에 자동으로 a가 생성됨
    5. B에서 GameManager의 Start() 실행
    6. B가 PhotonNetwork.Instantiate()에 의해 플레이어 캐릭터 b를 A와 B에 생성

    **************************************************/

    // 게임 시작과 동시에 플레이어가 될 게임 오브젝트를 생성
    private void Start()
    {
        // 생성할 랜덤 위치 지정
        Vector3 randomSpawnPos = Random.insideUnitSphere * 5f;
        // 위치 y값은 0으로 변경
        randomSpawnPos.y = 0f;

        // 네트워크 상의 모든 클라이언트들에서 생성 실행
        // 단, 해당 게임 오브젝트의 주도권은, 생성 메서드를 직접 실행한 클라이언트에게 있음
        PhotonNetwork.Instantiate(playerPrefab.name, randomSpawnPos, Quaternion.identity);
    }

    // 점수를 추가하고 UI 갱신
    public void AddScore(int newScore)
    {
        // 게임 오버가 아닌 상태에서만 점수 증가 가능
        if (!isGameover)
        {
            // 점수 추가
            score += newScore;
            // 점수 UI 텍스트 갱신
            UIManager.instance.UpdateScoreText(score);
        }
    }

    // 게임 오버 처리
    public void EndGame()
    {
        // 게임 오버 상태를 참으로 변경
        isGameover = true;
        // 게임 오버 UI를 활성화
        UIManager.instance.SetActiveGameoverUI(true);
    }

    // 키보드 입력을 감지하고 룸을 나가게 함
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PhotonNetwork.LeaveRoom();
        }
    }

    // 룸을 나갈때 자동 실행되는 메서드
    public override void OnLeftRoom()
    {
        // 룸을 나가면 로비 씬으로 돌아감
        SceneManager.LoadScene("Lobby");
    }
}