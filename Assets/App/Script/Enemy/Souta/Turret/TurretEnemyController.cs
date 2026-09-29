using UnityEngine;
using System.Collections;

public class TurretEnemyController : MonoBehaviour, IHitReceiver, IHitSource
{
    [Header("References")]
    [SerializeField] private Transform turretHead;
    [SerializeField] private Transform muzzle;
    [SerializeField] private LineRenderer laser;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject receiveCollision;
    [SerializeField] private GameObject chainCollision;

    [SerializeField] private float hp = 10.0f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 15.0f;
    [SerializeField] private float aimTime = 0.7f;
    [SerializeField] private float cooldownTime = 1.5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Aim Rotation")]
    [SerializeField] private Transform aimPivot;
    [SerializeField] private float rotateSpeed = 8f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 45f;

    [Header("Effect")]
    [SerializeField] private EffectPlayer effectPlayer;

    private bool isDead = false;

    private Transform player;
    private State m_CurrentState;

    [Header("Aim")]
    [SerializeField] private float aimHeight = 0.5f;//エイム位置の高さ調整

    [Header("Detection")]
    [SerializeField] private LayerMask sightMask;

    public Transform Player => player;
    public float AttackRange => attackRange;
    public float AimTime => aimTime;
    public float CooldownTime => cooldownTime;
    public LineRenderer Laser => laser;
    public Transform Muzzle => muzzle;

    private void Start()
    {
        GameObject playerObj =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        if (laser != null)
        {
            laser.enabled = false;
        }

        ChangeState(new IdleState(this));
    }

    private void Update()
    {
        if (isDead)
            return;

        m_CurrentState?.Update();
        //if(hp <= 0f)
        //{
        //    gameObject.SetActive(false);
        //}
    }

    public void ChangeState(State nextState)
    {
        m_CurrentState?.Exit();

        m_CurrentState = nextState;

        m_CurrentState?.Enter();
    }

    //プレイヤーが攻撃範囲内にいるかどうかを判定する処理
    public bool IsPlayerInRange()
    {
        if (player == null)
            return false;

        Vector3 start = muzzle.position;
        Vector3 target =
            player.position - Vector3.up * aimHeight;

        Vector3 dir = target - start;
        float distance = dir.magnitude;

        if (distance > attackRange)
            return false;

        dir.Normalize();

        if (Physics.Raycast(
            start,
            dir,
            out RaycastHit hit,
            distance,
            sightMask))
        {
            return hit.transform == player;
        }

        return false;
    }

    //プレイヤー方向に砲台を回転させる処理
    public void RotateToPlayer()
    {
        if (player == null || aimPivot == null)
            return;

        Vector3 targetPos =
            player.position - Vector3.up * aimHeight;

        // 本体は左右だけ回転
        Vector3 horizontalDir =
            targetPos - transform.position;

        horizontalDir.y = 0f;

        if (horizontalDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetYaw =
                Quaternion.LookRotation(horizontalDir);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetYaw,
                    Time.deltaTime * rotateSpeed);
        }

        // AimPivotは上下だけ回転
        Vector3 aimDir =
            targetPos - aimPivot.position;

        if (aimDir.sqrMagnitude < 0.001f)
            return;

        Vector3 localDir =
            transform.InverseTransformDirection(
                aimDir.normalized);

        float pitch =
            Mathf.Atan2(
                localDir.y,
                localDir.z) *
            Mathf.Rad2Deg;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch);

        Quaternion targetPitch =
            Quaternion.Euler(-pitch, 0f, 0f);

        aimPivot.localRotation =
            Quaternion.Slerp(
                aimPivot.localRotation,
                targetPitch,
                Time.deltaTime * rotateSpeed);
    }

    //照準レイザーの更新処理
    public void UpdateLaser()
    {
        if (laser == null)
            return;

        Vector3 start = muzzle.position;
        Vector3 dir = muzzle.forward;

        laser.SetPosition(0, start);

        if (Physics.Raycast(
            start,
            dir,
            out RaycastHit hit,
            attackRange))
        {
            laser.SetPosition(1, hit.point);
        }
        else
        {
            laser.SetPosition(
                1,
                start + dir * attackRange);
        }
    }

    private void CheckDeath()
    {
        if (hp > 0f || isDead)
            return;

        isDead = true;

        if (laser != null)
        {
            laser.enabled = false;
        }

        if (receiveCollision != null)
        {
            receiveCollision.SetActive(false);
        }

        if (chainCollision != null)
        {
            chainCollision.SetActive(false);
        }

        if (animator != null)
        {
            animator.SetTrigger("Damage");
        }
        else
        {
            AnimationEvent_DeathEnd();
        }
    }

    public void Shoot()
    {
        if (bulletPrefab == null)
            return;

        GameObject obj = Instantiate(
            bulletPrefab,
            muzzle.position,
            muzzle.rotation);

        EnemyBullet bullet = obj.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Initialize(transform);
        }
    }

    public void PlayAttackAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    public void AnimationEvent_Shoot()
    {
        AudioManager.Instance.PlaySe("SE_Shot", gameObject.transform.position);
        Shoot();
    }

    public void AnimationEvent_AttackEnd()
    {
        ChangeState(new CooldownState(this));
    }

    public void AnimationEvent_DeathEnd()
    {        
        effectPlayer.PlayAt(0, transform.position, Quaternion.identity);

        StartCoroutine(PlayExplosionSE());

        StartCoroutine(DestroyDelay());
    }

    private IEnumerator PlayExplosionSE()
    {
        yield return new WaitForSeconds(0.7f);      // 待機時間

        AudioManager.Instance.PlaySe("E_Explosion", transform.position);
    }

    private IEnumerator DestroyDelay()
    {
        yield return new WaitForSeconds(1.0f);

        gameObject.SetActive(false);
    }

    #region Hit Interface

    public void OnHit(HitEventData data)
    {
        if (isDead)
            return;

        if (data.payload is BlowPayload blow)
        {
            if (data.targetHitbox == receiveCollision)
            {
                hp -= blow.damageConstant * 40f;
                CheckDeath();
            }
        }

        if (data.payload is EnemyAttackPayload attack)
        {
            if (data.targetHitbox == chainCollision)
            {
                hp -= attack.damage;
                CheckDeath();
            }
        }
    }

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {

    }


    #endregion

    // Base State
    public abstract class State
    {
        protected TurretEnemyController owner;

        protected State(TurretEnemyController owner)
        {
            this.owner = owner;
        }

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }
    }

    // Idle
    public class IdleState : State
    {
        public IdleState(TurretEnemyController owner)
            : base(owner)
        {
        }

        public override void Update()
        {
            if (owner.IsPlayerInRange())
            {
                owner.ChangeState(
                    new AimState(owner));
            }
        }
    }
    // Aim
    public class AimState : State
    {
        private float timer;

        public AimState(TurretEnemyController owner)
            : base(owner)
        {
        }

        public override void Enter()
        {
            timer = owner.AimTime;

            if (owner.Laser != null)
            {
                owner.Laser.enabled = true;
            }
        }

        public override void Update()
        {
            if (!owner.IsPlayerInRange())
            {
                owner.ChangeState(
                    new IdleState(owner));
                return;
            }

            owner.RotateToPlayer();
            owner.UpdateLaser();

            timer -= Time.deltaTime;

            if (timer <= 0)
            {
                owner.ChangeState(
                    new AttackState(owner));
            }
        }

        public override void Exit()
        {
            if (owner.Laser != null)
            {
                owner.Laser.enabled = false;
            }
        }
    }

    // Attack
    public class AttackState : State
    {
        public AttackState(TurretEnemyController owner)
            : base(owner)
        {
        }

        public override void Enter()
        {
            owner.RotateToPlayer();
            owner.PlayAttackAnimation();
        }

        public override void Update()
        {
            //攻撃アニメーション終了イベントを待つ
        }
    }

    // Cooldown
    public class CooldownState : State
    {
        private float timer;

        public CooldownState(TurretEnemyController owner)
            : base(owner)
        {
        }

        public override void Enter()
        {
            timer = owner.CooldownTime;
        }

        public override void Update()
        {
            if (!owner.IsPlayerInRange())
            {
                owner.ChangeState(
                    new IdleState(owner));
                return;
            }

            owner.RotateToPlayer();

            timer -= Time.deltaTime;

            if (timer <= 0)
            {
                owner.ChangeState(
                    new AimState(owner));
            }
        }
    }

}