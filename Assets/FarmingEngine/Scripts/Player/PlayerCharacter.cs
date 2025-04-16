using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FarmingEngine
{

    public enum PlayerInteractBehavior
    {
        MoveAndInteract = 0, // 当点击对象时，角色将自动移动到对象位置，然后与之交互
        InteractOnly = 10, // 当点击对象时，只有在交互范围内才会进行交互（不会自动移动）
    }

    /// <summary>
    /// 主角角色脚本，包含了移动和玩家控制/命令的代码
    /// </summary>

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerCharacterCombat))]
    [RequireComponent(typeof(PlayerCharacterAttribute))]
    [RequireComponent(typeof(PlayerCharacterInventory))]
    [RequireComponent(typeof(PlayerCharacterCraft))]
    public class PlayerCharacter : MonoBehaviour
    {
        public int player_id = 0;

        [Header("Movement")]
        public bool move_enabled = true; // 如果要使用自定义角色控制器，请禁用此选项
        public float move_speed = 4f; // 移动速度
        public float move_accel = 8; // 加速度
        public float rotate_speed = 180f; // 旋转速度
        public float fall_speed = 20f; // 下落速度
        public float fall_gravity = 40f; // 下落加速度
        public float slope_angle_max = 45f; // 角色能够攀爬的最大角度（单位：度）
        public float moving_threshold = 0.15f; // 移动阈值，角色被视为在移动（触发动画等）之前需要达到的速度
        public float ground_detect_dist = 0.1f; // 角色与地面的间距，用于检测角色是否在地面上
        public LayerMask ground_layer = ~0; // 定义什么是地面的层级掩码
        public bool use_navmesh = false; // 是否使用导航网格（NavMesh）

        [Header("Interact")]
        public PlayerInteractBehavior interact_type = PlayerInteractBehavior.MoveAndInteract; // 交互类型
        public float interact_range = 0f; // 添加到可选使用范围中的交互范围
        public float interact_offset = 0f; // 不要与角色中心交互，而是与前方的偏移量进行交互
        public bool action_ui; // 执行动作时是否显示动作计时器UI


        public UnityAction<string, float> onTriggerAnim; // 动作触发的事件回调

        private Rigidbody rigid; // 刚体组件，用于物理运算
        private CapsuleCollider collide; // 胶囊碰撞器，用于碰撞检测
        private PlayerCharacterAttribute character_attr; // 玩家角色属性
        private PlayerCharacterCombat character_combat; // 玩家角色战斗系统
        private PlayerCharacterCraft character_craft; // 玩家角色制作系统
        private PlayerCharacterInventory character_inventory; // 玩家角色背包系统
        private PlayerCharacterJump character_jump; // 玩家角色跳跃系统
        private PlayerCharacterSwim character_swim; // 玩家角色游泳系统
        private PlayerCharacterClimb character_climb; // 玩家角色攀爬系统
        private PlayerCharacterRide character_ride; // 玩家角色骑乘系统
        private PlayerCharacterHoe character_hoe; // 玩家角色锄地系统
        private PlayerCharacterAnim character_anim; // 玩家角色动画系统

        private Vector3 move; // 当前移动方向
        private Vector3 facing; // 当前朝向
        private Vector3 move_average; // 移动平均值
        private Vector3 prev_pos; // 上一帧的位置
        private Vector3 fall_vect; // 下落向量

        private bool auto_move = false; // 自动移动标志
        private Vector3 auto_move_pos; // 自动移动目标位置
        private Vector3 auto_move_pos_next; // 下一个自动移动目标位置
        private Selectable auto_move_target = null; // 自动移动目标
        private Destructible auto_move_attack = null; // 自动移动攻击目标

        private int auto_move_drop = -1; // 自动移动丢弃的物品索引
        private InventoryData auto_move_drop_inventory; // 自动移动丢弃的物品数据
        private float auto_move_timer = 0f; // 自动移动计时器

        private Vector3 ground_normal = Vector3.up; // 地面法线（默认向上）
        private bool controls_enabled = true; // 控制是否启用
        private bool movement_enabled = true; // 移动是否启用

        private bool is_grounded = false; // 是否接触地面
        private bool is_fronted = false; // 是否面对前方
        private bool is_busy = false; // 是否忙碌
        private bool is_sleep = false; // 是否睡觉
        private bool is_fishing = false; // 是否在钓鱼

        private Vector3 controls_move; // 控制移动方向
        private Vector3 controls_freelook; // 控制自由观察方向

        private ActionSleep sleep_target = null; // 睡觉目标
        private Coroutine action_routine = null; // 当前执行的协程
        private GameObject action_progress = null; // 动作进度指示器
        private bool can_cancel_action = false; // 是否可以取消当前动作

        private Vector3[] nav_paths = new Vector3[0]; // 导航路径
        private int path_index = 0; // 当前路径索引
        private bool calculating_path = false; // 是否正在计算路径
        private bool path_found = false; // 是否找到路径

        private static PlayerCharacter player_first = null; // 第一个玩家角色（用于排序）
        private static List<PlayerCharacter> players_list = new List<PlayerCharacter>(); // 玩家角色列表

        void Awake()
        {
            if (player_first == null || player_id < player_first.player_id)
                player_first = this; // 记录第一个玩家角色

            players_list.Add(this); // 将当前角色添加到玩家列表
            rigid = GetComponent<Rigidbody>(); // 获取刚体组件
            collide = GetComponentInChildren<CapsuleCollider>(); // 获取胶囊碰撞器
            character_attr = GetComponent<PlayerCharacterAttribute>(); // 获取角色属性
            character_combat = GetComponent<PlayerCharacterCombat>(); // 获取角色战斗系统
            character_craft = GetComponent<PlayerCharacterCraft>(); // 获取角色制作系统
            character_inventory = GetComponent<PlayerCharacterInventory>(); // 获取角色背包系统
            character_jump = GetComponent<PlayerCharacterJump>(); // 获取角色跳跃系统
            character_swim = GetComponent<PlayerCharacterSwim>(); // 获取角色游泳系统
            character_climb = GetComponent<PlayerCharacterClimb>(); // 获取角色攀爬系统
            character_ride = GetComponent<PlayerCharacterRide>(); // 获取角色骑乘系统
            character_hoe = GetComponent<PlayerCharacterHoe>(); // 获取角色锄地系统
            character_anim = GetComponent<PlayerCharacterAnim>(); // 获取角色动画系统
            facing = transform.forward; // 初始化角色的朝向
            prev_pos = transform.position; // 初始化角色的上一个位置
            fall_vect = Vector3.down * fall_speed; // 初始化下落向量

            TheGame.Find().onNewDay += OnNewDay; // 在 Awake 中注册新的一天事件（Start 中会调用此事件）
        }

        private void OnDestroy()
        {
            players_list.Remove(this); // 当对象被销毁时，从玩家列表中移除
        }

        private void Start()
        {
            PlayerControlsMouse mouse_controls = PlayerControlsMouse.Get(); // 获取鼠标控制实例
            mouse_controls.onClickFloor += OnClickFloor; // 注册点击地面事件
            mouse_controls.onClickObject += OnClickObject; // 注册点击对象事件
            mouse_controls.onClick += OnClick; // 注册点击事件
            mouse_controls.onRightClick += OnRightClick; // 注册右键点击事件
            mouse_controls.onHold += OnMouseHold; // 注册按住事件
            mouse_controls.onRelease += OnMouseRelease; // 注册释放事件

            TheGame.Get().onPause += OnPause; // 注册暂停事件

            if (player_id < 0)
                Debug.LogError("Player ID should be 0 or more: -1 is reserved to indicate neutral (no player)"); // 检查玩家 ID 是否有效
        }


        private void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            //保存位置
            SaveData.position = GetPosition();

            if (IsDead() || !move_enabled)
                return;

            //检查是否到达移动的终点
            UpdateEndAutoMove();
            UpdateEndActions();

            UpdateControls();
        }

        void FixedUpdate()
        {
            // 如果游戏暂停，直接返回
            if (TheGame.Get().IsPaused())
                return;

            // 根据导航网格路径或移动目标更新自动移动目标位置
            UpdateAutoMoveTarget();

            // 检测是否接触地面
            DetectGrounded();
            // 检测是否面对前方
            DetectFronted();

            // 计算角色应移动的方向
            Vector3 tmove = FindMovementDirection();

            // 应用之前计算的移动向量
            move = Vector3.Lerp(move, tmove, move_accel * Time.fixedDeltaTime);
            rigid.velocity = move;

            // 计算角色应朝向的方向
            Vector3 tfacing = FindFacingDirection();
            // 如果朝向方向的大小大于0.5，则更新朝向
            if (tfacing.magnitude > 0.5f)
                facing = tfacing;

            // 应用朝向
            Quaternion targ_rot = Quaternion.LookRotation(facing, Vector3.up);
            // 使刚体旋转朝向目标旋转方向
            rigid.MoveRotation(Quaternion.RotateTowards(rigid.rotation, targ_rot, rotate_speed * Time.fixedDeltaTime));

            // 检查平均移动距离（用于判断角色是否被卡住）
            Vector3 last_frame_travel = transform.position - prev_pos;
            move_average = Vector3.MoveTowards(move_average, last_frame_travel, 1f * Time.fixedDeltaTime);
            prev_pos = transform.position;

            // 停止自动移动
            // 如果移动平均值的大小小于0.02，并且自动移动计时器大于1秒，则认为角色被卡住
            bool stuck_somewhere = move_average.magnitude < 0.02f && auto_move_timer > 1f;
            if (stuck_somewhere)
                auto_move = false;
        }


        private void UpdateControls()
        {
            // 如果控制未启用，则直接返回
            if (!IsControlsEnabled())
                return;

            // 获取玩家控制相关的组件
            PlayerControls controls = PlayerControls.Get(player_id); // 玩家控制（键盘/手柄）
            PlayerControlsMouse mcontrols = PlayerControlsMouse.Get(); // 鼠标控制
            JoystickMobile joystick = JoystickMobile.Get(); // 移动端的虚拟摇杆
            KeyControlsUI ui_controls = KeyControlsUI.Get(player_id); // UI 控件

            // 获取控制的移动和自由观察方向
            Vector2 cmove = controls.GetMove(); // 获取移动方向
            Vector2 cfree = controls.GetFreelook(); // 获取自由观察方向
            controls_move = new Vector3(cmove.x, 0f, cmove.y); // 将移动方向转换为三维向量
            controls_freelook = new Vector3(cfree.x, 0f, cfree.y); // 将自由观察方向转换为三维向量

            // 检查虚拟摇杆是否激活
            bool joystick_active = joystick != null && joystick.IsActive();
            if (joystick_active && !character_craft.IsBuildMode())
                controls_move += new Vector3(joystick.GetDir().x, 0f, joystick.GetDir().y); // 如果虚拟摇杆激活且不在建造模式中，更新移动方向
            if (!controls.IsGamePad())
                controls_freelook = Vector3.zero; // 如果不是手柄控制，则将自由观察方向重置为零

            // 根据相机的朝向旋转移动和自由观察方向
            controls_move = TheCamera.Get().GetFacingRotation() * controls_move;
            controls_freelook = TheCamera.Get().GetFacingRotation() * controls_freelook;

            // 检查是否有面板焦点
            bool panel_focus = controls.gamepad_controls && ui_controls != null && ui_controls.IsPanelFocus();
            if (!panel_focus && !is_busy)
            {
                // 按下动作按钮
                if (controls.IsPressAction())
                {
                    if (character_craft.CanBuild())
                        character_craft.StartCraftBuilding(); // 如果可以建造，则开始建造
                    else
                        InteractWithNearest(); // 否则与最近的对象互动
                }

                // 按下攻击按钮
                if (Combat.CanAttack() && controls.IsPressAttack())
                    Attack(); // 执行攻击

                // 按下跳跃按钮
                if (character_jump != null && controls.IsPressJump())
                    character_jump.Jump(); // 执行跳跃
            }

            // 开始建造
            if (controls.IsPressUISelect() && !is_busy && character_craft.CanBuild())
                character_craft.StartCraftBuilding(); // 如果按下 UI 选择按钮且不忙碌并且可以建造，则开始建造

            // 当使用键盘/摇杆/手柄移动时停止点击自动移动
            if (controls.IsMoving() || mcontrols.IsDoubleTouch() || joystick_active)
                StopAutoMove(); // 停止自动移动

            // 如果在移动时取消动作
            bool is_moving_controls = auto_move || controls.IsMoving() || joystick_active;
            if (is_busy && can_cancel_action && is_moving_controls)
                CancelBusy(); // 如果忙碌且可以取消且在移动中，取消当前动作

            // 停止睡眠
            if (is_busy || IsMoving() || sleep_target == null)
                StopSleep(); // 如果忙碌、正在移动或没有睡眠目标，则停止睡眠
        }

        private void UpdateEndAutoMove()
        {
            // 如果没有自动移动或正在忙碌，则直接返回
            if (!auto_move || is_busy)
                return;

            Vector3 move_dir = auto_move_pos - GetInteractCenter(); // 计算自动移动位置与交互中心的距离
            Buildable current_buildable = character_craft.GetCurrentBuildable(); // 获取当前可建造的物体
            if (auto_move_target != null)
            {
                // 当接近目标时激活选择对象
                if (move_dir.magnitude < auto_move_target.GetUseRange(this))
                {
                    auto_move = false;
                    auto_move_target.Use(this, auto_move_pos); // 使用目标并停止自动移动
                    auto_move_target = null;
                }
            }
            else if (current_buildable != null && character_craft.ClickedBuild())
            {
                // 当接近点击的建造位置时完成建造
                if (current_buildable != null && move_dir.magnitude < current_buildable.GetBuildRange(this))
                {
                    auto_move = false;
                    character_craft.StartCraftBuilding(auto_move_pos); // 开始建造
                }
            }
            else if (move_dir.magnitude < moving_threshold * 2f)
            {
                // 当接近点击位置时停止移动并丢弃物品
                auto_move = false;
                character_inventory.DropItem(auto_move_drop_inventory, auto_move_drop); // 丢弃物品
            }
        }

        private void UpdateEndActions()
        {
            // 如果目标不能再被攻击（工具坏了或目标死了），则停止攻击
            if (!character_combat.CanAttack(auto_move_attack))
                auto_move_attack = null;

            // 停止睡眠
            if (is_busy || IsMoving() || sleep_target == null)
                StopSleep(); // 如果忙碌、正在移动或没有睡眠目标，则停止睡眠
        }

        private void UpdateAutoMoveTarget()
        {
            // 如果移动未启用，则直接返回
            if (!IsMovementEnabled())
                return;

            // 更新移动目标位置
            GameObject auto_move_obj = GetAutoTarget(); // 获取自动移动目标
            if (auto_move && auto_move_obj != null)
            {
                Vector3 diff = auto_move_obj.transform.position - auto_move_pos; // 计算目标位置与当前自动移动位置的差距
                if (diff.magnitude > 1f)
                {
                    auto_move_pos = auto_move_obj.transform.position;
                    auto_move_pos_next = auto_move_obj.transform.position;
                    CalculateNavmesh(); // 目标移动时重新计算导航网格
                }
            }

            // 如果使用导航网格且路径已找到，则计算下一路径
            if (auto_move && use_navmesh && path_found && path_index < nav_paths.Length)
            {
                auto_move_pos_next = nav_paths[path_index];
                Vector3 move_dir_total = auto_move_pos_next - transform.position; // 计算当前位置到下一路径点的方向
                move_dir_total.y = 0f; // 只考虑水平移动
                if (move_dir_total.magnitude < 0.2f)
                    path_index++; // 如果接近下一路径点，更新路径索引
            }
        }


        // 查找角色的移动方向
        private Vector3 FindMovementDirection()
        {
            Vector3 tmove = Vector3.zero;

            // 如果当前不允许移动，则直接返回零向量
            if (!IsMovementEnabled())
                return tmove;

            // 自动移动（鼠标点击后）
            auto_move_timer += Time.fixedDeltaTime;
            if (auto_move && auto_move_timer > 0.02f) // auto_move_timer 使得导航网格有足够的时间计算路径
            {
                // 计算从当前位置到目标位置的方向
                Vector3 move_dir_total = auto_move_pos - transform.position;
                Vector3 move_dir_next = auto_move_pos_next - transform.position;
                // 计算下一个目标点的方向，并限制最大移动距离为 1
                Vector3 move_dir = move_dir_next.normalized * Mathf.Min(move_dir_total.magnitude, 1f);
                move_dir.y = 0f; // 只考虑水平移动方向

                // 计算移动距离，确保不超过最大速度
                float move_dist = Mathf.Min(GetMoveSpeed(), move_dir.magnitude * 10f);
                tmove = move_dir.normalized * move_dist; // 最终的移动方向向量
            }

            // 键盘/手柄控制移动
            if (!auto_move && IsControlsEnabled())
            {
                tmove = controls_move * GetMoveSpeed(); // 根据控制输入的方向和移动速度计算移动向量
            }

            // 如果正在执行某个动作，则停止移动
            if (is_busy)
                tmove = Vector3.zero;

            // 如果没有跳跃并且不在地面上，则施加重力
            if (!IsJumping() && !is_grounded)
                fall_vect = Vector3.MoveTowards(fall_vect, Vector3.down * fall_speed, fall_gravity * Time.fixedDeltaTime);

            // 如果处于空中或正在跳跃，则应用重力
            if (!is_grounded || IsJumping())
            {
                tmove += fall_vect; // 添加重力加速度
            }
            // 如果在地面上，则考虑地面坡度
            else if (is_grounded)
            {
                tmove = Vector3.ProjectOnPlane(tmove.normalized, ground_normal).normalized * tmove.magnitude; // 修正移动方向以适应地面坡度
            }

            return tmove; // 返回最终的移动方向
        }

        // 查找角色的面向方向
        private Vector3 FindFacingDirection()
        {
            Vector3 tfacing = Vector3.zero;

            // 如果当前不允许移动，则直接返回零向量
            if (!IsMovementEnabled())
                return tfacing;

            // 如果正在移动，计算面向方向
            if (IsMoving())
            {
                tfacing = new Vector3(move.x, 0f, move.z).normalized; // 只考虑水平面上的方向
            }

            // 如果不是自由旋转模式，则使用右摇杆来调整角色的面向方向
            bool freerotate = TheCamera.Get().IsFreelook();
            if (!freerotate)
            {
                Vector2 look = controls_freelook;
                if (look.magnitude > 0.5f)
                    tfacing = look.normalized; // 根据控制器的摇杆输入设置面向方向
            }

            return tfacing; // 返回最终的面向方向
        }

        // 每次更换新的一天时，重置角色属性
        private void OnNewDay()
        {
            // 重置生命值和能量值
            Attributes.ResetAttribute(AttributeType.Health);
            Attributes.ResetAttribute(AttributeType.Energy);
        }


        // 暂停游戏时处理角色的速度（停止角色的运动）
        private void OnPause(bool paused)
        {
            if (paused)
            {
                rigid.velocity = Vector3.zero; // 如果游戏暂停，停止角色的物理速度
            }
        }

        // 检测角色是否在地面上
        private void DetectGrounded()
        {
            float hradius = GetColliderHeightRadius(); // 获取角色碰撞体的高度半径
            float radius = GetColliderRadius() * 0.9f; // 获取角色碰撞体的半径，并稍微缩小
            Vector3 center = GetColliderCenter(); // 获取角色碰撞体的中心位置

            float gdist; Vector3 gnormal;
            // 使用物理工具检测角色是否在地面上
            is_grounded = PhysicsTool.DetectGround(transform, center, hradius, radius, ground_layer, out gdist, out gnormal);
            ground_normal = gnormal;

            // 计算地面与水平面的夹角
            float slope_angle = Vector3.Angle(ground_normal, Vector3.up);
            // 如果角度大于最大坡度角，则认为角色不在地面上
            is_grounded = is_grounded && slope_angle <= slope_angle_max;
        }

        // 检测角色前方是否有障碍物
        private void DetectFronted()
        {
            Vector3 scale = transform.lossyScale; // 获取角色的缩放比例
            float hradius = collide.height * scale.y * 0.5f - 0.02f; // 半径为碰撞体高度的一半减去偏移
            float radius = collide.radius * (scale.x + scale.y) * 0.5f + 0.5f; // 计算角色碰撞体的半径

            Vector3 center = GetColliderCenter(); // 获取碰撞体的中心
            Vector3 p1 = center; // 碰撞体的中心点
            Vector3 p2 = center + Vector3.up * hradius; // 在碰撞体上方的点
            Vector3 p3 = center + Vector3.down * hradius; // 在碰撞体下方的点

            RaycastHit h1, h2, h3;
            // 使用射线检测角色前方是否有障碍物
            bool f1 = PhysicsTool.RaycastCollision(p1, facing * radius, out h1);
            bool f2 = PhysicsTool.RaycastCollision(p2, facing * radius, out h2);
            bool f3 = PhysicsTool.RaycastCollision(p3, facing * radius, out h3);

            is_fronted = f1 || f2 || f3; // 如果前方有障碍物，则设置为 true
        }

        // --- 通用动作 --- 

        // 与触发动作相同，但会显示进度圆圈
        public void TriggerProgressBusy(float duration, UnityAction callback = null)
        {
            if (!is_busy) // 如果当前没有忙碌
            {
                // 如果有 UI，并且持续时间大于 0.1s，则显示进度圆圈
                if (action_ui && AssetData.Get().action_progress != null && duration > 0.1f)
                {
                    action_progress = Instantiate(AssetData.Get().action_progress, transform);
                    action_progress.GetComponent<ActionProgress>().duration = duration;
                }

                is_busy = true; // 设置为忙碌状态
                action_routine = StartCoroutine(RunBusyRoutine(duration, callback)); // 启动忙碌协程
                can_cancel_action = true; // 允许取消动作
                StopMove(); // 停止移动
            }
        }

        // 等待 X 秒进行任何通用动作（玩家在此期间不能进行其他操作）
        public void TriggerBusy(float duration, UnityAction callback = null)
        {
            if (!is_busy) // 如果当前没有忙碌
            {
                is_busy = true; // 设置为忙碌状态
                action_routine = StartCoroutine(RunBusyRoutine(duration, callback)); // 启动忙碌协程
                can_cancel_action = false; // 不允许取消动作
            }
        }

        // 忙碌状态的协程，持续指定的时间
        private IEnumerator RunBusyRoutine(float action_duration, UnityAction callback = null)
        {
            yield return new WaitForSeconds(action_duration); // 等待指定时间

            is_busy = false; // 动作完成后设置为空闲状态
            if (callback != null)
                callback.Invoke(); // 调用回调方法（如果有的话）
        }

        // 取消当前的忙碌状态
        public void CancelBusy()
        {
            if (can_cancel_action && is_busy) // 如果可以取消并且当前处于忙碌状态
            {
                if (action_routine != null)
                    StopCoroutine(action_routine); // 停止协程
                if (action_progress != null)
                    Destroy(action_progress); // 销毁进度条
                is_busy = false; // 设置为空闲状态
                is_fishing = false; // 停止钓鱼
            }
        }

        // 直接调用动画
        public void TriggerAnim(string anim, Vector3 face_at, float duration = 0f)
        {
            FaceTorward(face_at); // 朝向目标位置
            if (onTriggerAnim != null)
                onTriggerAnim.Invoke(anim, duration); // 调用动画触发事件
        }

        // 设置角色的忙碌状态
        public void SetBusy(bool action)
        {
            is_busy = action; // 设置为忙碌或空闲
            can_cancel_action = false; // 不允许取消动作
        }

        // --- 特殊动作 ---

        // 启动睡觉动作
        public void Sleep(ActionSleep sleep_target)
        {
            if (!is_sleep && IsMovementEnabled()) // 如果角色当前没有在睡觉且允许移动
            {
                this.sleep_target = sleep_target; // 设置睡觉目标
                is_sleep = true; // 设置为睡觉状态
                auto_move = false; // 停止自动移动
                auto_move_attack = null; // 清空自动攻击目标
                TheGame.Get().SetGameSpeedMultiplier(sleep_target.sleep_speed_mult); // 设置游戏速度倍数
            }
        }

        // 停止睡觉动作
        public void StopSleep()
        {
            if (is_sleep) // 如果当前正在睡觉
            {
                is_sleep = false; // 设置为非睡觉状态
                sleep_target = null; // 清空睡觉目标
                TheGame.Get().SetGameSpeedMultiplier(1f); // 恢复正常游戏速度
            }
        }

        // 从钓鱼点钓取物品
        public void FishItem(ItemProvider source, int quantity, float duration)
        {
            if (source != null && source.HasItem()) // 如果钓鱼点存在并且有物品
            {
                is_fishing = true; // 设置为钓鱼状态

                if (source != null)
                    FaceTorward(source.transform.position); // 朝向钓鱼点的位置

                TriggerBusy(0.4f, () =>
                {
                    action_routine = StartCoroutine(FishRoutine(source, quantity, duration)); // 启动钓鱼协程
                });
            }
        }

        // 钓鱼过程的协程
        private IEnumerator FishRoutine(ItemProvider source, int quantity, float duration)
        {
            is_fishing = true; // 设置为钓鱼状态

            float timer = 0f;
            while (is_fishing && timer < duration) // 在钓鱼时间内持续执行
            {
                yield return new WaitForSeconds(0.02f); // 每帧等待 0.02 秒
                timer += 0.02f; // 累加时间

                if (IsMoving()) // 如果在钓鱼过程中移动，则停止钓鱼
                    is_fishing = false;
            }

            if (is_fishing) // 如果钓鱼未中断
            {
                source.RemoveItem(); // 从钓鱼点移除物品
                source.GainItem(this, quantity); // 获取物品
            }

            is_fishing = false; // 钓鱼完成
        }


        //----- 玩家命令 -----------

        public void MoveTo(Vector3 pos)
        {
            auto_move = true; // 启动自动移动
            auto_move_pos = pos; // 设置目标位置
            auto_move_pos_next = pos; // 设置下一个目标位置
            auto_move_target = null; // 清除自动移动目标
            auto_move_attack = null; // 清除自动攻击目标
            auto_move_drop = -1; // 清除自动丢弃目标
            auto_move_drop_inventory = null; // 清除自动丢弃物品
            auto_move_timer = 0f; // 重置计时器
            path_found = false; // 路径未找到
            calculating_path = false; // 路径计算未开始

            CalculateNavmesh(); // 计算导航网格，找到可行路径
        }

        public void UpdateMoveTo(Vector3 pos)
        {
            // 每帧调用该方法，避免重新计算导航网格
            auto_move = true; // 启动自动移动
            auto_move_pos = pos; // 设置目标位置
            auto_move_pos_next = pos; // 设置下一个目标位置
            path_found = false; // 路径未找到
            calculating_path = false; // 路径计算未开始
            auto_move_target = null; // 清除自动移动目标
            auto_move_attack = null; // 清除自动攻击目标
            auto_move_drop = -1; // 清除自动丢弃目标
            auto_move_drop_inventory = null; // 清除自动丢弃物品
        }

        public void FaceFront()
        {
            // 朝向相机的前方
            FaceTorward(transform.position + TheCamera.Get().GetFacingFront());
        }

        public void FaceTorward(Vector3 pos)
        {
            Vector3 face = (pos - transform.position); // 计算面向方向
            face.y = 0f; // 保持水平面上的方向
            if (face.magnitude > 0.01f) // 如果目标方向有足够的距离
            {
                facing = face.normalized; // 设置面向方向
            }
        }

        // 与可交互对象交互
        public void Interact(Selectable selectable)
        {
            Interact(selectable, selectable.GetClosestInteractPoint(GetInteractCenter()));
        }

        // 与可交互对象在指定位置交互
        public void Interact(Selectable selectable, Vector3 pos)
        {
            if (interact_type == PlayerInteractBehavior.MoveAndInteract)
                InteractMove(selectable, pos); // 移动并交互
            else if (interact_type == PlayerInteractBehavior.InteractOnly)
                InteractDirect(selectable, pos); // 仅交互，不移动
        }

        // 直接与对象交互（不移动）
        public void InteractDirect(Selectable selectable, Vector3 pos)
        {
            if (selectable.IsInUseRange(this)) // 如果在交互范围内
                selectable.Use(this, pos); // 执行交互
        }

        // 移动到目标并与其交互
        public void InteractMove(Selectable selectable, Vector3 pos)
        {
            bool can_interact = selectable.CanBeInteracted(); // 检查是否可以交互
            Vector3 tpos = pos;
            if (can_interact)
                tpos = selectable.GetClosestInteractPoint(GetInteractCenter(), pos); // 获取最近的交互点

            auto_move_target = can_interact ? selectable : null; // 设置自动移动目标
            auto_move_pos = tpos; // 设置目标位置
            auto_move_pos_next = tpos; // 设置下一个目标位置

            auto_move = true; // 启动自动移动
            auto_move_drop = -1; // 清除自动丢弃目标
            auto_move_drop_inventory = null; // 清除自动丢弃物品
            auto_move_timer = 0f; // 重置计时器
            path_found = false; // 路径未找到
            calculating_path = false; // 路径计算未开始
            auto_move_attack = null; // 清除自动攻击目标
            CalculateNavmesh(); // 计算导航网格，找到可行路径
        }

        // 与最近的可交互对象交互
        public void InteractWithNearest()
        {
            bool freelook = TheCamera.Get().IsFreelook(); // 判断是否是自由视角模式
            Selectable nearest = null;

            if (freelook)
            {
                nearest = Selectable.GetNearestRaycast(); // 获取最近的可交互对象（自由视角下）
            }
            else
            {
                nearest = Selectable.GetNearestAutoInteract(GetInteractCenter(), 5f); // 获取最近的自动交互对象
            }

            if (nearest != null)
            {
                Interact(nearest); // 与最近的对象交互
            }
        }

        public void Attack()
        {
            if (Combat.attack_type == PlayerAttackBehavior.ClickToHit)
                AttackFront(); // 点击攻击
            else
                AttackNearest(); // 攻击最近的目标
        }

        public void AttackFront()
        {
            if (TheCamera.Get().IsFreelook()) // 如果是自由视角模式
                FaceFront(); // 朝向前方
            Combat.Attack(); // 执行攻击
        }

        public void Attack(Destructible target)
        {
            if (interact_type == PlayerInteractBehavior.MoveAndInteract)
                AttackMove(target); // 移动并攻击
            else if (Combat.attack_type == PlayerAttackBehavior.AutoAttack)
                AttackTarget(target); // 自动攻击目标
            else
                AttackDirect(target); // 直接攻击目标
        }

        // 仅执行一次攻击（不移动）
        public void AttackDirect(Destructible target)
        {
            if (Combat.IsAttackTargetInRange(target)) // 如果目标在攻击范围内
                Combat.Attack(target); // 执行攻击
        }

        // 移动到目标并攻击
        public void AttackMove(Destructible target)
        {
            if (character_combat.CanAttack(target)) // 如果可以攻击目标
            {
                auto_move = true; // 启动自动移动
                auto_move_target = null; // 清除自动移动目标
                auto_move_attack = target; // 设置自动攻击目标
                auto_move_pos = target.transform.position; // 设置目标位置
                auto_move_pos_next = target.transform.position; // 设置下一个目标位置
                auto_move_drop = -1; // 清除自动丢弃目标
                auto_move_drop_inventory = null; // 清除自动丢弃物品
                auto_move_timer = 0f; // 重置计时器
                path_found = false; // 路径未找到
                calculating_path = false; // 路径计算未开始
                CalculateNavmesh(); // 计算导航网格，找到可行路径
            }
        }

        // 目标进行多次攻击，但不移动
        public void AttackTarget(Destructible target)
        {
            if (character_combat.CanAttack(target)) // 如果可以攻击目标
            {
                auto_move = false; // 不进行移动
                auto_move_target = null; // 清除自动移动目标
                auto_move_attack = target; // 设置攻击目标
                auto_move_pos = transform.position; // 设置当前位置为目标位置
                auto_move_pos_next = transform.position; // 设置下一个目标位置
                auto_move_drop = -1; // 清除自动丢弃目标
                auto_move_drop_inventory = null; // 清除自动丢弃物品
                auto_move_timer = 0f; // 重置计时器
                path_found = false; // 路径未找到
                calculating_path = false; // 路径计算未开始
            }
        }

        public void AttackNearest()
        {
            float range = Mathf.Max(Combat.GetAttackRange() + 2f, 5f); // 计算攻击范围
            Destructible destruct = Destructible.GetNearestAutoAttack(this, GetInteractCenter(), range); // 获取最近的可攻击目标
            Attack(destruct); // 攻击目标
        }

        public void StopMove()
        {
            StopAutoMove(); // 停止自动移动
            move = Vector3.zero; // 设置移动速度为零
            rigid.velocity = Vector3.zero; // 停止物理运动
        }

        public void StopAutoMove()
        {
            auto_move = false; // 停止自动移动
            auto_move_target = null; // 清除自动移动目标
            auto_move_attack = null; // 清除自动攻击目标
            auto_move_drop_inventory = null; // 清除自动丢弃物品
        }

        // 暂停自动移动，但保持目标位置
        public void PauseAutoMove()
        {
            auto_move = false; // 停止自动移动
        }

        public void ResumeAutoMove()
        {
            if (auto_move_target != null || auto_move_attack != null)
                auto_move = true; // 恢复自动移动
        }

        public void SetFallVect(Vector3 fall)
        {
            fall_vect = fall; // 设置下落向量
        }

        public void Kill()
        {
            character_combat.Kill(); // 执行角色死亡操作
        }

        public void EnableControls()
        {
            controls_enabled = true; // 启用角色控制
        }


        //----- 控制管理 -----------

        public void DisableControls()
        {
            controls_enabled = false; // 禁用控制
            StopAutoMove(); // 停止自动移动
        }

        public void EnableMovement()
        {
            movement_enabled = true; // 启用角色移动
        }

        public void DisableMovement()
        {
            movement_enabled = false; // 禁用角色移动
            StopAutoMove(); // 停止自动移动
        }

        public void EnableCollider()
        {
            collide.enabled = true; // 启用碰撞体
        }

        public void DisableCollider()
        {
            collide.enabled = false; // 禁用碰撞体
        }

        //------- 鼠标点击事件 --------

        private void OnClick(Vector3 pos)
        {
            if (!IsControlsEnabled()) // 如果控制被禁用，返回
                return;

            bool freerotate = TheCamera.Get().IsFreelook(); // 检查是否为自由视角模式
            if (freerotate)
                AttackFront(); // 如果是自由视角，执行前方攻击
        }

        private void OnRightClick(Vector3 pos)
        {
            if (!IsControlsEnabled()) // 如果控制被禁用，返回
                return;

            // 右键点击事件（目前没有实现具体功能）
        }

        private void OnMouseHold(Vector3 pos)
        {
            if (!IsControlsEnabled()) // 如果控制被禁用，返回
                return;

            if (TheGame.IsMobile()) // 如果是移动设备，返回
                return; // 移动设备使用摇杆而非鼠标按住

            // 如果按住鼠标并且当前有自动移动，且按住时间超过1秒，停止自动移动
            PlayerControlsMouse mcontrols = PlayerControlsMouse.Get();
            if (auto_move && mcontrols.GetMouseHoldDuration() > 1f)
                StopAutoMove();

            // 只有在正常移动时按住鼠标才会更新目标位置，如果正在交互，则不改变目标位置
            if (character_craft.GetCurrentBuildable() == null && auto_move_target == null && auto_move_attack == null)
            {
                UpdateMoveTo(pos); // 更新移动目标位置
            }
        }

        private void OnMouseRelease(Vector3 pos)
        {
            if (!IsControlsEnabled()) // 如果控制被禁用，返回
                return;

            bool in_range = interact_type == PlayerInteractBehavior.MoveAndInteract || character_craft.IsInBuildRange();
            if (TheGame.IsMobile() && in_range)
            {
                character_craft.TryBuildAt(pos); // 如果是移动设备并且在建造范围内，尝试建造
            }
        }

        private void OnClickFloor(Vector3 pos)
        {
            if (!IsControlsEnabled()) // 如果控制被禁用，返回
                return;

            CancelBusy(); // 取消任何繁忙状态

            // 如果是建造模式
            if (character_craft.IsBuildMode())
            {
                if (character_craft.ClickedBuild()) // 如果点击了可建造位置
                    character_craft.CancelCrafting(); // 取消建造

                if (!TheGame.IsMobile()) // 如果不是移动设备，直接尝试建造
                    character_craft.TryBuildAt(pos);
            }
            // 如果不是建造模式，移动到点击位置
            else if (interact_type == PlayerInteractBehavior.MoveAndInteract)
            {
                MoveTo(pos); // 移动到目标位置

                PlayerUI ui = PlayerUI.Get(player_id);
                auto_move_drop = ui != null ? ui.GetSelectedSlotIndex() : -1; // 获取选中物品的槽位索引
                auto_move_drop_inventory = ui != null ? ui.GetSelectedSlotInventory() : null; // 获取选中物品的库存
            }
            else
            {
                character_hoe?.HoeGroundAuto(pos); // 如果不是建造模式，使用锄头自动锄地
            }
        }

        private void OnClickObject(Selectable selectable, Vector3 pos)
        {
            if (!IsControlsEnabled()) // 如果控制被禁用，返回
                return;

            if (selectable == null) // 如果选择的对象为空，返回
                return;

            if (character_craft.IsBuildMode()) // 如果处于建造模式
            {
                OnClickFloor(pos); // 执行点击地面逻辑
                return;
            }

            CancelBusy(); // 取消任何繁忙状态
            selectable.Select(); // 选择该对象

            // 判断是否攻击目标
            bool freerotate = TheCamera.Get().IsFreelook(); // 检查是否为自由视角模式
            Destructible target = selectable.Destructible; // 获取可破坏对象
            if (freerotate)
            {
                AttackFront(); // 如果是自由视角模式，执行前方攻击
            }
            else if (target != null && character_combat.CanAutoAttack(target))
            {
                Attack(target); // 如果目标可自动攻击，执行攻击
            }
            else
            {
                Interact(selectable, pos); // 否则与目标进行交互
            }
        }

        //---- 导航网格 ----

        public void CalculateNavmesh()
        {
            if (auto_move && use_navmesh && !calculating_path) // 如果启用了自动移动，且需要使用导航网格，且没有正在计算路径
            {
                calculating_path = true; // 设置正在计算路径
                path_found = false; // 设置路径未找到
                path_index = 0; // 设置路径索引为0
                auto_move_pos_next = auto_move_pos; // 默认目标位置
                NavMeshTool.CalculatePath(transform.position, auto_move_pos, 1 << 0, FinishCalculateNavmesh); // 计算路径
            }
        }

        private void FinishCalculateNavmesh(NavMeshToolPath path)
        {
            calculating_path = false; // 路径计算结束
            path_found = path.success; // 设置路径是否找到
            nav_paths = path.path; // 获取计算得到的路径
            path_index = 0; // 设置路径索引为0
        }

        //---- 获取器 ----

        // 检查角色是否接近某个指定组的物体
        public bool IsNearGroup(GroupData group)
        {
            Selectable group_select = Selectable.GetNearestGroup(group, transform.position); // 获取离角色最近的该组对象
            return group_select != null && group_select.IsInUseRange(this); // 判断是否在交互范围内
        }

        public ActionSleep GetSleepTarget()
        {
            return sleep_target; // 获取当前睡觉目标
        }

        public Destructible GetAutoAttackTarget()
        {
            return auto_move_attack; // 获取自动攻击目标
        }

        public Selectable GetAutoSelectTarget()
        {
            return auto_move_target; // 获取自动选择目标
        }

        // 获取自动目标对象
        public GameObject GetAutoTarget()
        {
            GameObject auto_move_obj = null;
            if (auto_move_target != null && auto_move_target.type == SelectableType.Interact)
                auto_move_obj = auto_move_target.gameObject; // 如果自动目标是可交互类型，获取其游戏对象
            if (auto_move_attack != null)
                auto_move_obj = auto_move_attack.gameObject; // 如果有攻击目标，获取其游戏对象
            return auto_move_obj; // 返回自动目标对象
        }

        // 获取自动丢弃物品的库存数据
        public InventoryData GetAutoDropInventory()
        {
            return auto_move_drop_inventory; // 获取自动丢弃物品的库存数据
        }

        // 获取自动移动的目标位置
        public Vector3 GetAutoMoveTarget()
        {
            return auto_move_pos; // 获取自动移动的目标位置
        }

        // 检查角色是否死亡
        public bool IsDead()
        {
            return character_combat.IsDead(); // 检查角色是否死亡
        }

        // 检查角色是否处于睡眠状态
        public bool IsSleeping()
        {
            return is_sleep; // 返回角色是否在睡眠状态
        }

        // 检查角色是否正在钓鱼
        public bool IsFishing()
        {
            return is_fishing; // 返回角色是否在钓鱼
        }

        // 检查角色是否骑乘
        public bool IsRiding()
        {
            return character_ride != null && character_ride.IsRiding(); // 如果骑乘对象不为空且角色正在骑乘，返回true
        }

        // 检查角色是否在游泳
        public bool IsSwimming()
        {
            return character_swim != null && character_swim.IsSwimming(); // 如果游泳对象不为空且角色正在游泳，返回true
        }

        // 检查角色是否在攀爬
        public bool IsClimbing()
        {
            return character_climb != null && character_climb.IsClimbing(); // 如果攀爬对象不为空且角色正在攀爬，返回true
        }

        // 检查角色是否在跳跃
        public bool IsJumping()
        {
            return character_jump != null && character_jump.IsJumping(); // 如果跳跃对象不为空且角色正在跳跃，返回true
        }

        // 检查是否正在进行自动移动
        public bool IsAutoMove()
        {
            return auto_move; // 如果正在自动移动，返回true
        }

        // 检查角色是否繁忙（例如，正在执行某些操作）
        public bool IsBusy()
        {
            return is_busy; // 如果角色处于繁忙状态，返回true
        }

        // 检查角色是否在移动
        public bool IsMoving()
        {
            if (IsRiding() && character_ride.GetAnimal() != null)
                return character_ride.GetAnimal().IsMoving(); // 如果正在骑乘动物，检查动物是否在移动
            if (Climbing && Climbing.IsClimbing())
                return Climbing.IsMoving(); // 如果正在攀爬，检查是否在移动

            Vector3 moveXZ = new Vector3(move.x, 0f, move.z); // 只检查水平移动
            return moveXZ.magnitude > GetMoveSpeed() * moving_threshold; // 如果水平移动速度大于阈值，返回true
        }

        // 获取角色当前的移动向量
        public Vector3 GetMove()
        {
            return move; // 返回角色的移动向量
        }

        // 获取角色当前的朝向
        public Vector3 GetFacing()
        {
            return facing; // 返回角色的朝向向量
        }

        // 获取角色的标准化移动向量（归一化后，并根据速度限制）
        public Vector3 GetMoveNormalized()
        {
            return move.normalized * Mathf.Clamp01(move.magnitude / GetMoveSpeed()); // 返回归一化后的移动向量
        }

        // 获取角色的移动速度（考虑加速和不同的状态）
        public float GetMoveSpeed()
        {
            float boost = 1f + character_attr.GetBonusEffectTotal(BonusType.SpeedBoost); // 获取所有速度加成
            float base_speed = IsSwimming() ? character_swim.swim_speed : move_speed; // 如果在游泳状态，使用游泳速度，否则使用基础移动速度
            return base_speed * boost * character_attr.GetSpeedMult(); // 计算并返回最终的移动速度
        }

        // 获取角色当前的位置
        public Vector3 GetPosition()
        {
            if (IsRiding() && character_ride.GetAnimal() != null)
                return character_ride.GetAnimal().transform.position; // 如果在骑乘状态，返回骑乘动物的位置
            return transform.position; // 否则返回角色自身的位置
        }

        // 获取角色的交互中心位置
        public Vector3 GetInteractCenter()
        {
            return GetPosition() + transform.forward * interact_offset; // 返回角色的交互中心位置，前方偏移
        }

        // 获取角色碰撞体的中心位置
        public Vector3 GetColliderCenter()
        {
            Vector3 scale = transform.lossyScale; // 获取角色的缩放因子
            return collide.transform.position + Vector3.Scale(collide.center, scale); // 返回碰撞体的实际中心位置
        }

        // 获取角色碰撞体的半径高度
        public float GetColliderHeightRadius()
        {
            Vector3 scale = transform.lossyScale; // 获取角色的缩放因子
            return collide.height * scale.y * 0.5f + ground_detect_dist; // 半径是碰撞体高度的一半减去偏移量
        }

        // 获取角色碰撞体的半径
        public float GetColliderRadius()
        {
            Vector3 scale = transform.lossyScale; // 获取角色的缩放因子
            return collide.radius * (scale.x + scale.y) * 0.5f; // 返回碰撞体的半径
        }

        // 检查角色是否面朝前方
        public bool IsFronted()
        {
            return is_fronted; // 返回角色是否面朝前方
        }

        // 检查角色是否在地面上
        public bool IsGrounded()
        {
            return is_grounded; // 返回角色是否处于地面状态
        }

        // 检查是否可以控制角色
        public bool IsControlsEnabled()
        {
            return move_enabled && controls_enabled && !IsDead() && !TheUI.Get().IsFullPanelOpened(); // 只有当移动、控制启用且角色未死亡，且没有打开全屏面板时，才可以控制角色
        }

        // 检查角色是否可以移动（是否有动作阻止其移动）
        public bool IsMovementEnabled()
        {
            return move_enabled && movement_enabled && !IsDead() && !IsRiding() && !IsClimbing(); // 角色必须启用移动并且未死亡，且不处于骑乘或攀爬状态才能移动
        }

        // 获取角色的战斗组件
        public PlayerCharacterCombat Combat
        {
            get { return character_combat; } // 返回角色的战斗组件
        }

        // 获取角色的属性组件
        public PlayerCharacterAttribute Attributes
        {
            get { return character_attr; } // 返回角色的属性组件
        }

        // 获取角色的建造组件
        public PlayerCharacterCraft Crafting
        {
            get { return character_craft; } // 返回角色的建造组件
        }

        // 获取角色的物品栏组件
        public PlayerCharacterInventory Inventory
        {
            get { return character_inventory; } // 返回角色的物品栏组件
        }

        // 获取角色的跳跃组件
        public PlayerCharacterJump Jumping
        {
            get { return character_jump; } // 返回角色的跳跃组件（可能为null）
        }

        // 获取角色的游泳组件
        public PlayerCharacterSwim Swimming
        {
            get { return character_swim; } // 返回角色的游泳组件（可能为null）
        }

        // 获取角色的攀爬组件
        public PlayerCharacterClimb Climbing
        {
            get { return character_climb; } // 返回角色的攀爬组件（可能为null）
        }

        // 获取角色的骑乘组件
        public PlayerCharacterRide Riding
        {
            get { return character_ride; } // 返回角色的骑乘组件（可能为null）
        }

        // 获取角色的动画组件
        public PlayerCharacterAnim Animation
        {
            get { return character_anim; } // 返回角色的动画组件（可能为null）
        }

        // 获取角色的保存数据
        public PlayerCharacterData Data => SaveData; // 兼容其他版本，等同于SaveData
        public PlayerCharacterData SData => SaveData; // 兼容其他版本，等同于SaveData

        // 获取角色的保存数据（保存的数据是基于玩家ID的）
        public PlayerCharacterData SaveData 
        {
            get { return PlayerCharacterData.Get(player_id); } // 获取角色保存的数据
        }

        // 获取角色的保存数据（兼容性版本）
        public PlayerCharacterData SavessData
        {
            get { return PlayerCharacterData.Get(player_id); } // 保持兼容性，等同于SaveData
        }

        // 获取角色的物品栏数据
        public InventoryData InventoryData
        {
            get { return character_inventory.InventoryData; } // 返回角色物品栏的数据
        }

        // 获取角色的装备数据
        public InventoryData EquipData
        {
            get { return character_inventory.EquipData; } // 返回角色装备数据
        }

        // 获取离指定位置最近的角色
        public static PlayerCharacter GetNearest(Vector3 pos, float range = 999f)
        {
            PlayerCharacter nearest = null;
            float min_dist = range;
            foreach (PlayerCharacter unit in players_list)
            {
                float dist = (unit.transform.position - pos).magnitude;
                if (dist < min_dist)
                {
                    min_dist = dist;
                    nearest = unit;
                }
            }
            return nearest; // 返回距离指定位置最近的角色
        }

        // 获取第一个玩家角色
        public static PlayerCharacter GetFirst()
        {
            return player_first; // 返回第一个玩家角色
        }

        // 根据玩家ID获取指定角色
        public static PlayerCharacter Get(int player_id = 0)
        {
            foreach (PlayerCharacter player in players_list)
            {
                if (player.player_id == player_id)
                    return player; // 返回指定ID的角色
            }
            return null; // 如果未找到，返回null
        }

        // 获取所有玩家角色
        public static List<PlayerCharacter> GetAll()
        {
            return players_list; // 返回所有玩家角色的列表
        }

    }

}