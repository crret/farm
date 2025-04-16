using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FarmingEngine
{
    /// <summary>
    /// 玩家角色锄地功能的类
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterHoe : MonoBehaviour
    {
        // 锄头物品的组数据
        public GroupData hoe_item;
        // 锄地建筑物的建造数据
        public ConstructionData hoe_soil;
        // 锄地范围
        public float hoe_range = 1f;
        // 锄地建造半径
        public float hoe_build_radius = 0.5f;
        // 锄地消耗的能量
        public int hoe_energy = 1;

        private PlayerCharacter character;

        void Awake()
        {
            character = GetComponent<PlayerCharacter>();
        }

        // 离开场景时调用的方法
        private void OnDestroy()
        {
            // 留空
        }

        // 开始时调用的方法
        private void Start()
        {
            // 留空
        }

        // 固定帧更新时调用的方法
        void FixedUpdate()
        {
            // 留空
        }

        // 每帧更新时调用的方法
        private void Update()
        {
            // 自动锄地
            if (character.IsAutoMove())
            {
                HoeGroundAuto(character.GetAutoMoveTarget());
            }

            PlayerControls control = PlayerControls.Get();
            if (control.IsPressAttack() && character.IsControlsEnabled())
            {
                // 获取锄地位置
                Vector3 hoe_pos = character.GetInteractCenter() + character.GetFacing() * 1f;
                HoeGround(hoe_pos);
            }
        }

        /// <summary>
        /// 锄地方法
        /// </summary>
        /// <param name="pos">锄地位置</param>
        public void HoeGround(Vector3 pos)
        {
            if (!CanHoe())
                return;

            character.StopMove();
            character.Attributes.AddAttribute(AttributeType.Energy, -hoe_energy);

            // 触发角色动画
            character.TriggerAnim(character.Animation ? character.Animation.hoe_anim : "", pos);
            character.TriggerBusy(0.8f, () =>
            {
                // 获取最近的建筑和植物
                Construction prev = Construction.GetNearest(pos, hoe_build_radius);
                Plant plant = Plant.GetNearest(pos, hoe_build_radius);

                // 如果存在先前的建筑且没有植物，则销毁它
                if (prev != null && plant == null && prev.data == hoe_soil)
                {
                    prev.Destroy();
                    return;
                }

                // 创建建筑对象
                Construction construct = Construction.CreateBuildMode(hoe_soil, pos);
                construct.GetBuildable().StartBuild(character);
                construct.GetBuildable().SetBuildPositionTemporary(pos);

                // 如果可以建造，则完成建造
                if (construct.GetBuildable().CheckIfCanBuild())
                {
                    construct.GetBuildable().FinishBuild();
                }
                else
                {
                    // 否则销毁建筑对象
                    Destroy(construct.gameObject);
                }
            });

        }

        /// <summary>
        /// 检查是否可以进行锄地操作
        /// </summary>
        /// <returns>是否可以锄地</returns>
        public bool CanHoe()
        {
            bool has_energy = character.Attributes.GetAttributeValue(AttributeType.Energy) >= hoe_energy;
            InventoryItemData ivdata = character.EquipData.GetEquippedItem(EquipSlot.Hand);
            ItemData idata = ItemData.Get(ivdata?.item_id);
            return has_energy && idata != null && idata.HasGroup(hoe_item) && !character.IsBusy();
        }

        /// <summary>
        /// 自动锄地方法
        /// </summary>
        /// <param name="pos">锄地位置</param>
        public void HoeGroundAuto(Vector3 pos)
        {
            Vector3 dir = pos - transform.position;
            if (character.IsBusy() || character.Crafting.ClickedBuild() || dir.magnitude > hoe_range
                || character.GetAutoSelectTarget() != null || character.GetAutoDropInventory() != null)
                return;

            InventoryItemData ivdata = character.EquipData.GetEquippedItem(EquipSlot.Hand);
            if (ivdata != null && CanHoe())
            {
                HoeGround(pos);

                // 减少物品耐久度
                if (ivdata != null)
                    ivdata.durability -= 1;
            }
        }
    }
}
