using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FarmingEngine
{
    /// <summary>
    /// 管理角色背包和相关操作的类
    /// 还负责生成/销毁装备的视觉附加效果
    /// </summary>

    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterInventory : MonoBehaviour
    {
        public int inventory_size = 15; // 如果你改变这个值，确保更新UI
        public ItemData[] starting_items; // 起始物品

        public UnityAction<Item> onTakeItem; // 拾取物品的回调
        public UnityAction<Item> onDropItem; // 丢弃物品的回调
        public UnityAction<ItemData> onGainItem; // 获得新物品的回调

        private PlayerCharacter character; // 角色对象

        private EquipAttach[] equip_attachments; // 装备附件数组

        private Dictionary<string, EquipItem> equipped_items = new Dictionary<string, EquipItem>(); // 已装备物品的字典

        void Awake()
        {
            character = GetComponent<PlayerCharacter>(); // 获取角色组件
            equip_attachments = GetComponentsInChildren<EquipAttach>(); // 获取所有装备附件组件
        }

        private void Start()
        {
            bool has_inventory = PlayerData.Get().HasInventory(character.player_id); // 判断玩家是否已有背包

            InventoryData.size = inventory_size; // 设置背包大小，这也会创建背包
            EquipData.size = 99; // 设置装备数据大小，大小不影响装备数据

            // 如果是新游戏，添加初始物品
            if (!has_inventory)
            {
                InventoryData invdata = InventoryData.Get(InventoryType.Inventory, character.player_id); // 获取玩家的背包数据
                foreach (ItemData item in starting_items)
                {
                    invdata.AddItem(item.id, 1, item.durability, UniqueID.GenerateUniqueID()); // 将初始物品添加到背包
                }
            }
        }

        void Update()
        {
            HashSet<string> equipped_data = new HashSet<string>(); // 存储已装备的物品数据
            List<string> remove_list = new List<string>(); // 存储需要卸下的物品ID

            // 处理装备物品
            foreach (KeyValuePair<int, InventoryItemData> item in EquipData.items)
            {
                if (item.Value != null)
                {
                    equipped_data.Add(item.Value.item_id); // 将已装备物品的ID加入集合
                    if (!equipped_items.ContainsKey(item.Value.item_id))
                        EquipAddedItem(item.Value.item_id); // 装备新物品
                }
            }

            // 创建卸下物品列表
            foreach (KeyValuePair<string, EquipItem> item in equipped_items)
            {
                if (!equipped_data.Contains(item.Key))
                    remove_list.Add(item.Key); // 如果物品没有在装备物品集合中，添加到移除列表
            }

            // 卸下物品
            foreach (string item_id in remove_list)
            {
                UnequipRemovedItem(item_id); // 卸下物品
            }
        }

        // ------- 物品相关功能 ----------

        // 拾取地上的物品
        public void TakeItem(Item item)
        {
            if (BagData != null && !InventoryData.CanTakeItem(item.data.id, item.quantity) && !item.data.IsBag())
            {
                TakeItem(BagData, item); // 如果主背包无法接收，转移到背包
            }
            else
            {
                TakeItem(InventoryData, item); // 拾取到主背包
            }
        }

        public void TakeItem(InventoryData inventory, Item item)
        {
            if (item != null && !character.IsBusy() && inventory.CanTakeItem(item.data.id, item.quantity))
            {
                character.FaceTorward(item.transform.position); // 角色面向物品

                if (onTakeItem != null)
                    onTakeItem.Invoke(item); // 触发拾取物品的回调

                character.TriggerBusy(0.4f, () =>
                {
                    // 确保物品在0.4秒的动画时间内未被销毁
                    if (item != null && inventory.CanTakeItem(item.data.id, item.quantity))
                    {
                        PlayerData pdata = PlayerData.Get();
                        DroppedItemData dropped_item = pdata.GetDroppedItem(item.GetUID());
                        float durability = dropped_item != null ? dropped_item.durability : item.data.durability; // 获取物品耐久度
                        int slot = inventory.AddItem(item.data.id, item.quantity, durability, item.GetUID()); // 将物品添加到背包

                        ItemTakeFX.DoTakeFX(item.transform.position, item.data, inventory.type, slot); // 播放拾取特效

                        item.TakeItem(); // 销毁物品
                    }
                });
            }
        }

        // 自动拾取物品，不进行动画、面向、动作
        public void AutoTakeItem(Item item)
        {
            if (BagData != null && !InventoryData.CanTakeItem(item.data.id, item.quantity) && !item.data.IsBag())
            {
                AutoTakeItem(BagData, item); // 自动拾取到背包
            }
            else
            {
                AutoTakeItem(InventoryData, item); // 自动拾取到主背包
            }
        }

        public void AutoTakeItem(InventoryData inventory, Item item)
        {
            if (item != null && !character.IsBusy() && inventory.CanTakeItem(item.data.id, item.quantity))
            {
                PlayerData pdata = PlayerData.Get();
                DroppedItemData dropped_item = pdata.GetDroppedItem(item.GetUID());
                float durability = dropped_item != null ? dropped_item.durability : item.data.durability;
                int slot = inventory.AddItem(item.data.id, item.quantity, durability, item.GetUID()); // 添加到背包

                ItemTakeFX.DoTakeFX(item.transform.position, item.data, inventory.type, slot); // 播放拾取特效

                item.TakeItem(); // 销毁物品
            }
        }

        // 直接将新物品添加到背包
        public void GainItem(ItemData item, int quantity=1)
        {
            GainItem(item, quantity, transform.position); // 物品添加到背包，并指定来源位置
        }

        public void GainItem(ItemData item, int quantity, Vector3 source_pos)
        {
            if (BagData != null && !InventoryData.CanTakeItem(item.id, quantity) && !item.IsBag())
            {
                GainItem(BagData, item, quantity, source_pos); // 添加到背包
            }
            else
            {
                GainItem(InventoryData, item, quantity, source_pos); // 添加到主背包
            }
        }

        // 将物品添加到指定背包
        public void GainItem(InventoryData inventory, ItemData item, int quantity=1)
        {
            GainItem(inventory, item, quantity, transform.position); // 添加物品到指定背包
        }

        public void GainItem(InventoryData inventory, ItemData item, int quantity, Vector3 source_pos)
        {
            if (item != null)
            {
                if (inventory.CanTakeItem(item.id, quantity))
                {
                    if (inventory.type == InventoryType.Equipment)
                    {
                        EquipData.EquipItem(item.equip_slot, item.id, item.durability, UniqueID.GenerateUniqueID()); // 装备物品
                        ItemTakeFX.DoTakeFX(source_pos, item, inventory.type, (int)item.equip_slot); // 播放装备特效
                    }
                    else
                    {
                        int islot = inventory.AddItem(item.id, quantity, item.durability, UniqueID.GenerateUniqueID()); // 添加物品到背包
                        ItemTakeFX.DoTakeFX(source_pos, item, inventory.type, islot); // 播放拾取特效
                    }
                }
                else
                {
                    Item.Create(item, character.GetPosition(), quantity); // 如果背包满了，则将物品放到地上
                }
            }
        }

        // 使用物品并立即在地图上建造（跳过建造模式）
        public void BuildItem(int slot)
        {
            BuildItem(InventoryData, slot); // 通过背包槽位使用物品
        }

        public void BuildItem(InventoryData inventory, int slot)
        {
            character.Crafting.BuildItem(inventory, slot); // 调用角色的建造系统
            PlayerUI.Get(character.player_id)?.CancelSelection(); // 取消选择
        }

        // 吃掉物品并获得其属性
        public void EatItem(int slot)
        {
            EatItem(InventoryData, slot); // 通过背包槽位食用物品
        }

        public void EatItem(InventoryData inventory, int slot)
        {
            InventoryItemData idata = inventory.GetInventoryItem(slot); // 获取背包中的物品
            ItemData item = ItemData.Get(idata?.item_id); // 获取物品数据
            if (item != null && item.type == ItemType.Consumable)
            {
                if (inventory.IsItemIn(item.id, slot)) // 判断物品是否存在
                {
                    inventory.RemoveItemAt(slot, 1); // 移除物品
                    if (item.container_data) 
                        inventory.AddItem(item.container_data.id, 1, item.container_data.durability, UniqueID.GenerateUniqueID()); // 将容器物品添加回背包

                    character.StopSleep(); // 停止睡眠
                    character.Attributes.AddAttribute(AttributeType.Health, item.eat_hp); // 增加健康
                    character.Attributes.AddAttribute(AttributeType.Energy, item.eat_energy); // 增加能量
                    character.Attributes.AddAttribute(AttributeType.Hunger, item.eat_hunger); // 减少饥饿
                    character.Attributes.AddAttribute(AttributeType.Thirst, item.eat_thirst); // 减少口渴
                    character.Attributes.AddAttribute(AttributeType.Happiness, item.eat_happiness); // 增加幸福感

                    foreach (BonusEffectData bonus in item.eat_bonus)
                    {
                        character.SaveData.AddTimedBonus(bonus.type, bonus.value, item.eat_bonus_duration); // 添加奖励效果
                    }
                }
            }
        }

        // 丢弃物品到地面
        public void DropItem(int slot)
        {
            DropItem(InventoryData, slot); // 通过槽位丢弃物品
        }

        public void DropItem(InventoryData inventory, int slot)
        {
            InventoryItemData invdata = inventory?.GetInventoryItem(slot); // 获取物品数据
            ItemData idata = ItemData.Get(invdata?.item_id); // 获取物品ID
            if (invdata != null && idata != null && invdata.quantity > 0)
            {
                if (idata.CanBeDropped()) // 判断物品是否可以丢弃
                {
                    inventory.RemoveItemAt(slot, invdata.quantity); // 移除物品
                    Item iitem = Item.Create(idata, character.GetPosition(), invdata.quantity, invdata.durability, invdata.uid); // 在地面创建物品

                    PlayerUI.Get(character.player_id)?.CancelSelection(); // 取消选择

                    if (onDropItem != null)
                        onDropItem.Invoke(iitem); // 触发丢弃物品的回调
                }
                else if (idata.CanBeBuilt()) // 如果物品可以被建造
                {
                    BuildItem(inventory, slot); // 立即使用物品进行建造
                }
            }
        }

        // 从背包中移除物品，保留其容器
        public void UseItem(ItemData item, int quantity = 1)
        {
            if (item != null)
            {
                for (int i = 0; i < quantity; i++)
                {
                    if (InventoryData.HasItem(item.id, 1))
                        UseItem(InventoryData, item, 1); // 从主背包移除物品
                    else if (EquipData.HasItem(item.id, 1))
                        UseItem(EquipData, item, 1); // 从装备数据中移除物品
                    else if (BagData != null && BagData.HasItem(item.id, 1))
                        UseItem(BagData, item, 1); // 从背包中移除物品
                }
            }
        }

        // 从一个背包中移除物品，保留其容器
        public void UseItem(InventoryData inventory, ItemData item, int quantity = 1)
        {
            if (item != null)
            {
                inventory.RemoveItem(item.id, quantity); // 从背包中移除物品
                if (item.container_data) // 如果物品有容器数据
                    inventory.AddItem(item.container_data.id, quantity, item.container_data.durability, UniqueID.GenerateUniqueID()); // 将容器物品添加回背包
            }
        }


        // 从背包中直接移除指定组的物品，保留容器
        public void UseItemInGroup(GroupData group, int quantity = 1)
        {
            if (group != null)
            {
                for (int i = 0; i < quantity; i++)
                {
                    if (InventoryData.HasItemInGroup(group, 1))
                        UseItemInGroup(InventoryData, group, 1); // 从主背包中使用物品
                    else if (EquipData.HasItemInGroup(group, 1))
                        UseItemInGroup(EquipData, group, 1); // 从装备数据中使用物品
                    else if (BagData != null && BagData.HasItemInGroup(group, 1))
                        UseItemInGroup(BagData, group, 1); // 从背包中使用物品
                }
            }
        }

        // 在指定背包中移除指定组的物品，保留容器
        public void UseItemInGroup(InventoryData inventory, GroupData group, int quantity = 1)
        {
            if (group != null)
            {
                // 查找应使用的物品（按组）
                Dictionary<ItemData, int> remove_list = new Dictionary<ItemData, int>(); // 物品，数量
                foreach (KeyValuePair<int, InventoryItemData> pair in inventory.items)
                {
                    ItemData idata = ItemData.Get(pair.Value?.item_id);
                    if (idata != null && idata.HasGroup(group) && pair.Value.quantity > 0 && quantity > 0)
                    {
                        int remove = Mathf.Min(quantity, pair.Value.quantity); // 计算要移除的数量
                        remove_list.Add(idata, remove); // 添加到移除列表
                        quantity -= remove; // 更新剩余数量
                    }
                }

                // 使用这些特定的物品
                foreach (KeyValuePair<ItemData, int> pair in remove_list)
                {
                    UseItem(inventory, pair.Key, pair.Value); // 使用物品
                }
            }
        }

        // 兼容旧版本的函数：移除物品
        public void RemoveItem(ItemData item, int quantity = 1)
        {
            UseItem(item, quantity); // 移除物品
        }

        // 移除指定背包中的所有物品
        public void RemoveAll(InventoryData inventory)
        {
            inventory.RemoveAll(); // 清空背包
        }

        // ---- 装备相关 ----- 

        // 装备背包中的物品
        public void EquipItem(int islot)
        {
            InventoryItemData item = InventoryData.GetInventoryItem(islot); // 获取背包中的物品
            ItemData idata = ItemData.Get(item?.item_id); // 获取物品数据
            if (idata != null && idata.type == ItemType.Equipment) // 如果物品是装备类型
            {
                EquipItemTo(islot, idata.equip_slot); // 装备物品到指定槽位
            }
        }

        // 装备指定背包中的物品
        public void EquipItem(InventoryData inventory, int islot)
        {
            InventoryItemData item = inventory.GetInventoryItem(islot); // 获取指定背包槽位的物品
            ItemData idata = ItemData.Get(item?.item_id); // 获取物品数据
            if (idata != null && idata.type == ItemType.Equipment) // 如果物品是装备类型
            {
                EquipItemTo(inventory, islot, idata.equip_slot); // 装备物品到指定槽位
            }
        }

        // 卸下指定装备槽位的物品
        public void UnequipItem(EquipSlot eslot)
        {
            InventoryItemData invdata = EquipData.GetEquippedItem(eslot); // 获取装备槽位中的物品
            ItemData idata = ItemData.Get(invdata?.item_id); // 获取物品数据

            if (invdata != null && InventoryData.CanTakeItem(invdata.item_id, 1)) // 如果物品可以放入背包
            {
                EquipData.UnequipItem(eslot); // 卸下装备
                InventoryData.AddItem(invdata.item_id, 1, invdata.durability, invdata.uid); // 将物品添加到背包
            }
            else if (invdata != null && BagData != null && BagData.CanTakeItem(invdata.item_id, 1) && !idata.IsBag())
            {
                EquipData.UnequipItem(eslot); // 卸下装备
                BagData.AddItem(invdata.item_id, 1, invdata.durability, invdata.uid); // 将物品添加到背包
            }
        }

        // 移除指定装备槽位的物品
        public void RemoveEquipItem(EquipSlot eslot)
        {
            InventoryItemData invtem = EquipData.GetEquippedItem(eslot); // 获取指定槽位的装备物品
            ItemData idata = ItemData.Get(invtem?.item_id); // 获取物品数据
            if (idata != null)
            {
                EquipData.UnequipItem(eslot); // 卸下装备
                if (idata.container_data) // 如果物品有容器数据
                    EquipData.EquipItem(eslot, idata.container_data.id, idata.container_data.durability, UniqueID.GenerateUniqueID()); // 装备容器物品
            }
        }

        // 装备背包中的物品到指定装备槽位
        public void EquipItemTo(int islot, EquipSlot eslot)
        {
            EquipItemTo(InventoryData, islot, eslot); // 将物品装备到指定槽位
        }

        // 装备指定背包槽位的物品到指定装备槽位
        public void EquipItemTo(InventoryData inventory, int islot, EquipSlot eslot)
        {
            InventoryItemData invt_slot = inventory.GetInventoryItem(islot); // 获取背包槽位物品
            InventoryItemData invt_equip = EquipData.GetEquippedItem(eslot); // 获取当前装备槽位的物品
            ItemData idata = ItemData.Get(invt_slot?.item_id); // 获取物品数据
            ItemData edata = ItemData.Get(invt_equip?.item_id); // 获取已装备物品的数据

            if (invt_slot != null && inventory != EquipData && invt_slot.quantity > 0 && idata != null && eslot > 0)
            {
                if (edata == null)
                {
                    // 仅装备物品
                    EquipData.EquipItem(eslot, idata.id, invt_slot.durability, invt_slot.uid);
                    inventory.RemoveItemAt(islot, 1); // 从背包中移除物品
                }
                else if (invt_slot.quantity == 1 && idata.type == ItemType.Equipment)
                {
                    // 交换物品
                    inventory.RemoveItemAt(islot, 1); // 移除当前物品
                    EquipData.UnequipItem(eslot); // 卸下当前装备
                    EquipData.EquipItem(eslot, idata.id, invt_slot.durability, invt_slot.uid); // 装备新物品
                    inventory.AddItemAt(edata.id, islot, 1, invt_equip.durability, invt_equip.uid); // 将旧装备放回背包
                }
            }
        }

        // 卸下物品并放回指定槽位
        public void UnequipItemTo(EquipSlot eslot, int islot)
        {
            UnequipItemTo(InventoryData, eslot, islot); // 从背包中卸下并放回指定槽位
        }


        // 卸下指定装备槽位的物品并放回到指定背包槽位
        public void UnequipItemTo(InventoryData inventory, EquipSlot eslot, int islot)
        {
            InventoryItemData invt_slot = inventory.GetInventoryItem(islot); // 获取指定背包槽位的物品
            InventoryItemData invt_equip = EquipData.GetEquippedItem(eslot); // 获取当前装备槽位的物品
            ItemData idata = ItemData.Get(invt_slot?.item_id); // 获取背包物品数据
            ItemData edata = ItemData.Get(invt_equip?.item_id); // 获取已装备物品的数据
            bool both_bag = inventory.type == InventoryType.Bag && edata.IsBag(); // 判断是否为背包中的背包物品

            if (edata != null && inventory != EquipData && !both_bag)
            {
                bool same_item = idata != null && invt_slot != null && invt_slot.quantity > 0 && idata.id == edata.id && invt_slot.quantity < idata.inventory_max;
                bool slot_empty = invt_slot == null || invt_slot.quantity <= 0;
                if (same_item || slot_empty)
                {
                    // 卸下装备
                    EquipData.UnequipItem(eslot);
                    inventory.AddItemAt(edata.id, islot, 1, invt_equip.durability, invt_equip.uid); // 将物品放回到背包
                }
                else if (idata != null && invt_slot != null && !same_item && idata.type == ItemType.Equipment && idata.equip_slot == edata.equip_slot && invt_slot.quantity == 1)
                {
                    // 交换装备
                    inventory.RemoveItemAt(islot, 1); // 从背包移除物品
                    EquipData.UnequipItem(eslot); // 卸下当前装备
                    EquipData.EquipItem(eslot, idata.id, invt_slot.durability, invt_slot.uid); // 装备新的物品
                    inventory.AddItemAt(edata.id, islot, 1, invt_equip.durability, invt_equip.uid); // 将已装备的物品放回背包
                }
            }
        }

        // 更新所有装备物品的耐久度
        public void UpdateAllEquippedItemsDurability(bool weapon, float value)
        {
            // 遍历所有装备物品
            foreach (KeyValuePair<int, InventoryItemData> pair in EquipData.items)
            {
                InventoryItemData invdata = pair.Value;
                ItemData idata = ItemData.Get(invdata?.item_id); // 获取物品数据
                if (idata != null && invdata != null && idata.IsWeapon() == weapon && idata.durability_type == DurabilityType.UsageCount)
                    invdata.durability += value; // 更新耐久度
            }
        }

        // 更新指定装备槽位的物品耐久度
        public void UpdateDurability(EquipSlot eslot, float value)
        {
            InventoryItemData inv = GetEquippedItem(eslot); // 获取指定槽位的物品
            if (inv != null)
                inv.durability += value; // 更新耐久度
        }

        // 获取指定装备槽位的物品
        public InventoryItemData GetEquippedItem(EquipSlot eslot)
        {
            InventoryItemData invt_equip = EquipData.GetEquippedItem(eslot); // 获取装备槽位的物品
            return invt_equip;
        }

        // 获取指定装备槽位的物品数据
        public ItemData GetEquippedItemData(EquipSlot eslot)
        {
            InventoryItemData invt_equip = EquipData.GetEquippedItem(eslot); // 获取装备槽位的物品
            return invt_equip != null ? ItemData.Get(invt_equip.item_id) : null; // 返回物品数据
        }

        // 检查是否有物品装备在指定的装备槽位
        public bool HasEquippedItem(EquipSlot eslot)
        {
            return GetEquippedItem(eslot) != null; // 如果指定槽位有装备物品，返回true
        }

        // ---- 交换/合并物品 (slot1=选中的槽位, slot2=点击的槽位) -----

        // 移动物品：从一个槽位移到另一个槽位
        public void MoveItem(ItemSlot islot1, ItemSlot islot2, bool limit_one_item = false)
        {
            InventoryData inventory1 = islot1.GetInventory(); // 获取槽位1所在的背包
            InventoryData inventory2 = islot2.GetInventory(); // 获取槽位2所在的背包

            if (inventory1 == null || inventory2 == null)
                return;

            InventoryItemData iitem1 = islot1.GetInventoryItem(); // 获取槽位1的物品
            InventoryItemData iitem2 = islot2.GetInventoryItem(); // 获取槽位2的物品
            ItemData item1 = islot1.GetItem(); // 获取槽位1的物品数据
            ItemData item2 = islot2.GetItem(); // 获取槽位2的物品数据

            if (inventory2.type == InventoryType.Equipment)
            {
                EquipItem(inventory1, islot1.index); // 如果槽位2是装备槽，装备槽位1的物品
            }
            else if (inventory1.type == InventoryType.Equipment)
            {
                EquipSlot eslot = (EquipSlot)islot1.index;
                UnequipItemTo(inventory2, eslot, islot2.index); // 如果槽位1是装备槽，卸下物品并移动到槽位2
            }
            else if (item1 != null && item1 == item2 && !limit_one_item)
            {
                CombineItems(inventory1, islot1.index, inventory2, islot2.index); // 如果物品相同，合并物品
            }
            else if (item1 != item2)
            {
                // 交换物品
                int quant1 = iitem1 != null ? iitem1.quantity : 0;
                int quant2 = iitem2 != null ? iitem2.quantity : 0;
                bool quantity_is_1 = quant1 <= 1 && quant2 <= 1;
                bool can_swap = !limit_one_item || quantity_is_1 || item2 == null;
                if (can_swap)
                {
                    SwapItems(inventory1, islot1.index, inventory2, islot2.index); // 执行交换操作
                }
            }
        }

        // 交换两个背包槽位中的物品
        public void SwapItems(InventoryData inventory_data1, int slot1, InventoryData inventory_data2, int slot2)
        {
            PlayerData.Get().SwapInventoryItems(inventory_data1, slot1, inventory_data2, slot2); // 执行物品交换
        }

        // 合并两个槽位中的物品
        public void CombineItems(InventoryData inventory_data1, int slot1, InventoryData inventory_data2, int slot2)
        {
            InventoryItemData invdata1 = inventory_data1?.GetInventoryItem(slot1); // 获取槽位1的物品
            InventoryItemData invdata2 = inventory_data2?.GetInventoryItem(slot2); // 获取槽位2的物品
            ItemData idata1 = ItemData.Get(invdata1?.item_id); // 获取物品数据
            if (idata1 != null && invdata1.item_id == invdata2.item_id && (invdata1.quantity + invdata2.quantity) <= idata1.inventory_max)
                PlayerData.Get().CombineInventoryItems(inventory_data1, slot1, inventory_data2, slot2); // 如果符合条件，合并物品
        }

        // ---- 获取器 (多背包) ------

        // 检查是否在任何一个背包中有指定物品
        public bool HasItem(ItemData item, int quantity = 1)
        {
            int nb = CountItem(item); // 统计物品数量
            return nb >= quantity; // 如果数量足够，返回true
        }

        // 统计指定物品的数量
        public int CountItem(ItemData item)
        {
            return InventoryData.CountItem(item.id) 
                + EquipData.CountItem(item.id) 
                + (BagData != null ? BagData.CountItem(item.id) : 0); // 统计背包、装备和容器中的数量
        }

        // 检查是否在任何一个背包中有指定组的物品
        public bool HasItemInGroup(GroupData group, int quantity = 1)
        {
            return CountItemInGroup(group) >= quantity; // 检查组内物品的数量是否足够
        }

        // 统计指定组的物品数量
        public int CountItemInGroup(GroupData group)
        {
            return InventoryData.CountItemInGroup(group)
                + EquipData.CountItemInGroup(group)
                + (BagData != null ? BagData.CountItemInGroup(group) : 0); // 统计背包、装备和容器中的数量
        }

        // 检查背包或容器是否有空槽
        public bool HasEmptySlot()
        {
            return InventoryData.HasEmptySlot()
                || (BagData != null && BagData.HasEmptySlot()); // 如果有空槽，返回true
        }

        // 获取指定组中的第一个物品
        public InventoryItemData GetFirstItemInGroup(GroupData group)
        {
            if (InventoryData.HasItemInGroup(group))
                return InventoryData.GetFirstItemInGroup(group);
            if (EquipData.HasItemInGroup(group))
                return EquipData.GetFirstItemInGroup(group);
            if (BagData != null && BagData.HasItemInGroup(group))
                return BagData.GetFirstItemInGroup(group);
            return null;
        }

        // 检查是否能够拿到指定物品（检查背包或容器）
        public bool CanTakeItem(ItemData item, int quantity = 1)
        {
            return item != null && (InventoryData.CanTakeItem(item.id, quantity) 
                || (BagData != null && BagData.CanTakeItem(item.id, quantity))); // 判断是否可以拿到物品
        }


        // 获取装备的最佳背包（背包是可以容纳其他物品的物品）
        public InventoryItemData GetBestEquippedBag()
        {
            int best_size = 0; // 存储当前找到的背包最大容量
            InventoryItemData bag = null; // 存储最佳背包
            foreach (KeyValuePair<int, InventoryItemData> invdata in EquipData.items)
            {
                ItemData idata = invdata.Value?.GetItem(); // 获取装备物品的数据
                if(idata != null && idata.bag_size > best_size && !string.IsNullOrEmpty(invdata.Value.uid))
                {
                    best_size = idata.bag_size; // 更新最大容量
                    bag = invdata.Value; // 设置当前背包为最佳背包
                }
            }
            return bag; // 返回最佳背包
        }

        // 获取所有已装备的背包（背包是可以容纳其他物品的物品）
        public List<InventoryItemData> GetAllEquippedBags()
        {
            List<InventoryItemData> bags = new List<InventoryItemData>(); // 创建背包列表
            foreach (KeyValuePair<int, InventoryItemData> invdata in EquipData.items)
            {
                ItemData idata = invdata.Value?.GetItem(); // 获取物品数据
                if (idata != null && idata.IsBag() && !string.IsNullOrEmpty(invdata.Value.uid))
                {
                    bags.Add(invdata.Value); // 如果物品是背包，将其添加到列表中
                }
            }
            return bags; // 返回所有已装备的背包
        }

        // 获取可以容纳物品的有效背包（优先返回主背包，然后是背包）
        public InventoryData GetValidInventory(ItemData item, int quantity)
        {
            if (InventoryData.CanTakeItem(item.id, quantity))
                return InventoryData; // 如果主背包可以容纳物品，返回主背包
            else if(BagData != null && BagData.CanTakeItem(item.id, quantity))
                return BagData; // 否则，如果有背包且背包可以容纳物品，返回背包
            return null; // 如果两者都不能容纳物品，返回null
        }

        // --- 装备附件相关 ---

        // 装备一个新物品（例如：附加物品、装备等）
        private void EquipAddedItem(string item_id)
        {
            ItemData idata = ItemData.Get(item_id); // 获取物品数据
            if (idata != null && idata.equipped_prefab != null)
            {
                // 如果物品有Prefab（模型），创建并装备该物品
                GameObject equip_obj = Instantiate(idata.equipped_prefab, transform.position, Quaternion.identity);
                EquipItem eitem = equip_obj.GetComponent<EquipItem>(); // 获取物品的装备组件
                if (eitem != null)
                {
                    eitem.data = idata; // 设置装备物品的数据
                    eitem.target = GetEquipAttachment(idata.equip_slot, idata.equip_side); // 设置装备位置（基于槽位和侧面）
                    if (eitem.child_left != null)
                        eitem.target_left = GetEquipAttachment(idata.equip_slot, EquipSide.Left); // 设置左侧附件位置
                    if (eitem.child_right != null)
                        eitem.target_right = GetEquipAttachment(idata.equip_slot, EquipSide.Right); // 设置右侧附件位置
                }
                equipped_items.Add(item_id, eitem); // 将装备物品添加到已装备物品列表
            }
            else
            {
                equipped_items.Add(item_id, null); // 如果物品没有Prefab，直接添加到已装备物品列表（为null）
            }
        }

        // 卸下已移除的物品
        private void UnequipRemovedItem(string item_id)
        {
            if (equipped_items.ContainsKey(item_id))
            {
                EquipItem eitem = equipped_items[item_id]; // 获取已装备的物品
                equipped_items.Remove(item_id); // 从已装备物品列表中移除
                if (eitem != null)
                    Destroy(eitem.gameObject); // 销毁装备物品的GameObject
            }
        }

        // 获取角色上的装备附件（物品可以附着的身体位置）
        public EquipAttach GetEquipAttachment(EquipSlot slot, EquipSide side)
        {
            if (slot == EquipSlot.None)
                return null; // 如果没有指定槽位，返回null

            foreach (EquipAttach attach in equip_attachments) // 遍历所有的装备附件位置
            {
                if (attach.slot == slot)
                {
                    // 如果附件的侧面匹配，或者没有侧面要求，返回该附件
                    if (attach.side == EquipSide.Default || side == EquipSide.Default || attach.side == side)
                        return attach;
                }
            }
            return null; // 如果没有找到匹配的附件，返回null
        }

        // 获取第一个装备的武器模型（即装备的第一个武器）
        public EquipItem GetEquippedWeaponMesh()
        {
            InventoryItemData invdata = EquipData.GetEquippedWeapon(); // 获取装备的武器
            ItemData equipped = ItemData.Get(invdata?.item_id); // 获取物品数据
            if (equipped != null)
            {
                foreach (KeyValuePair<string, EquipItem> item in equipped_items)
                {
                    if (item.Key == equipped.id) // 查找已装备的物品
                        return item.Value; // 返回物品的装备模型（EquipItem）
                }
            }
            return null; // 如果没有装备武器，返回null
        }

        // 获取指定装备槽位的装备物品（即装备的物品的模型）
        public EquipItem GetEquippedItemMesh(EquipSlot slot)
        {
            InventoryItemData invdata = EquipData.GetEquippedItem(slot); // 获取指定槽位的装备物品
            ItemData equipped = ItemData.Get(invdata?.item_id); // 获取物品数据
            if (equipped != null)
            {
                foreach (KeyValuePair<string, EquipItem> item in equipped_items)
                {
                    if (item.Key == equipped.id) // 查找已装备的物品
                        return item.Value; // 返回物品的装备模型（EquipItem）
                }
            }
            return null; // 如果没有装备物品，返回null
        }

        // ---- 快捷方式 -----

        // 获取角色的主背包数据
        public InventoryData InventoryData
        {
            get { return InventoryData.Get(InventoryType.Inventory, character.player_id); }
        }

        // 获取角色的装备数据
        public InventoryData EquipData
        {
            get { return InventoryData.GetEquip(InventoryType.Equipment, character.player_id); }
        }

        // 获取角色的背包数据（如果没有背包则为null）
        public InventoryData BagData 
        {
            get {
                InventoryItemData bag = GetBestEquippedBag(); // 获取最佳背包
                return (bag != null) ? InventoryData.Get(InventoryType.Bag, bag.uid) : null; // 如果有背包，返回背包数据；否则返回null
            }
        }

        // 获取角色数据
        public PlayerCharacter GetCharacter()
        {
            return character; // 返回角色对象
        }

    }

}
