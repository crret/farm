using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarmingEngine
{
    /// <summary>
    /// 装备/卸下装备物品的操作
    /// </summary>
    
    [CreateAssetMenu(fileName = "Action", menuName = "FarmingEngine/Actions/Equip", order = 50)]
    public class ActionEquip : SAction
    {
        // 执行装备/卸下装备物品操作
        public override void DoAction(PlayerCharacter character, ItemSlot slot)
        {
            ItemData item = slot.GetItem(); // 获取物品槽中的物品数据
            InventoryData inventory = slot.GetInventory(); // 获取物品槽对应的物品栏数据

            if (item != null && item.type == ItemType.Equipment) // 如果物品存在且类型为装备
            {
                if (inventory.type == InventoryType.Equipment && slot is EquipSlotUI) // 如果物品栏类型为装备且物品槽是装备槽UI
                {
                    EquipSlotUI eslot = (EquipSlotUI)slot; // 转换为装备槽UI类型
                    character.Inventory.UnequipItem(eslot.equip_slot); // 卸下物品
                }
                else
                {
                    character.Inventory.EquipItem(inventory, slot.index); // 装备物品
                }
            }
        }

        // 判断是否可以进行装备/卸下装备物品操作的条件方法
        public override bool CanDoAction(PlayerCharacter character, ItemSlot slot)
        {
            ItemData item = slot.GetItem(); // 获取物品槽中的物品数据
            return item != null && item.type == ItemType.Equipment; // 只有当物品存在且类型为装备时才能进行操作
        }
    }

}