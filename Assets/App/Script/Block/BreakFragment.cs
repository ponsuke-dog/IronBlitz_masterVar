using UnityEngine;

public class BreakFragment : MonoBehaviour
{
    Vector3 velocity;
    Vector3 rotationSpeed;
    float timeScale;
    bool isGrounded = false;

    public void Init(Vector3 origin, float power, float scale)
    {
        // 飛ぶ方向
        Vector3 dir = (transform.position - origin).normalized;
        //if(dir.y < 0 )
        //    dir.y = 5.0f; // 少し上に飛ぶように補正
        Debug.Log($"dir: {dir}");
        // 初速
        velocity = dir * power;

        // ランダム回転
        rotationSpeed = Random.insideUnitSphere * 200f;

        timeScale = scale;
    }

    void Update()
    {
        // 重力
        if (!isGrounded)
            velocity += Physics.gravity * Time.deltaTime * timeScale;

        // 次の位置
        Vector3 nextPos = transform.position + velocity * Time.deltaTime * timeScale;

        //// 地面チェック（下方向にRay）
        //if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.001f))
        //{
        //    isGrounded = true;

        //    // 地面にピタッと合わせる
        //    transform.position = hit.point;

        //    return;
        //}

        // 通常移動
        transform.position = nextPos;

        // 回転
        transform.Rotate(rotationSpeed * Time.deltaTime * timeScale);

        //地面との接触判定


        //落下防止

    }

    void OnTriggerEnter(Collider other)
    {
        LayerMask layer = LayerMask.GetMask("Ground");
        if ((layer & (1 << other.gameObject.layer)) != 0)
        {
            Debug.Log("地面に当たった");

            isGrounded = true;
            velocity.y = 0; // 垂直方向の速度を止める
            rotationSpeed = Vector3.zero; // 回転も止める
        }
    }
}
