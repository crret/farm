using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FarmingEngine
{
    /// <summary>
    /// 负责管理玩家角色的制作和建造的类
    /// </summary>

    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerCharacterCraft : MonoBehaviour
    {
        // 用于存储所有的制作组数据
        public GroupData[] craft_groups;
        // 制作能量消耗
        public float craft_energy = 1f;
        // 建造能量消耗
        public float build_energy = 2f;

        // 制作和建造的回调事件
        public UnityAction<CraftData> onCraft;
        public UnityAction<Buildable> onBuild;

        private PlayerCharacter character; // 角色对象

        // 当前建造对象
        private Buildable current_buildable = null;
        // 当前制作数据
        private CraftData current_build_data = null;
        // 当前正在制作的数据
        private CraftData current_crafting = null;
        // 制作进度UI对象
        private GameObject craft_progress = null;
        // 建造计时器
        private float build_timer = 0f;
        // 制作计时器
        private float craft_timer = 0f;
        // 点击建造按钮标志
        private bool clicked_build = false;
        // 用于建造支付的物品栏槽位
        private InventorySlot build_pay_slot;

        // 初始化
        private void Awake()
        {
            character = GetComponent<PlayerCharacter>(); // 获取玩家角色组件
        }

        // 启动时设置UI回调
        void Start()
        {
            if (PlayerUI.Get(character.player_id))
            {
                PlayerUI.Get(character.player_id).onCancelSelection += CancelBuilding; // 取消选择时取消建造
            }
        }

        // 每帧更新
        void Update()
        {
            if (TheGame.Get().IsPaused()) // 游戏暂停时不执行
                return;

            if (character.IsDead()) // 角色死亡时不执行
                return;

            build_timer += Time.deltaTime; // 增加建造计时器
            craft_timer += Time.deltaTime; // 增加制作计时器

            PlayerControls controls = PlayerControls.Get(character.player_id);

            // 取消建造
            if (controls.IsPressUICancel() || controls.IsPressPause())
                CancelBuilding();

            // 取消制作
            if (current_crafting != null && character.IsMoving())
                CancelCrafting();

            // 完成制作计时
            if (current_crafting != null)
            {
                if (craft_timer > current_crafting.craft_duration)
                    CompleteCrafting(); // 如果超过制作时间，完成制作
            }
        }

        // ---- 制作功能 ----

        // 判断是否可以制作指定物品
        public bool CanCraft(CraftData item, bool skip_cost = false, bool skip_near = false)
        {
            if (item == null || character.IsDead())
                return false; // 如果物品为空或角色死亡，不能制作

            if (character.Attributes.GetAttributeValue(AttributeType.Energy) < craft_energy)
                return false; // 如果能量不足，不能制作

            bool has_craft_cost = skip_cost || HasCraftCost(item); // 是否有制作所需物品
            bool has_near = skip_near || HasCraftNear(item); // 是否在所需的建造附近
            return has_near && has_craft_cost; // 如果满足条件，返回true
        }

        // 带支付槽的制作判断
        public bool CanCraft(CraftData item, InventorySlot pay_slot, bool skip_near = false)
        {
            if (item == null || character.IsDead())
                return false; // 如果物品为空或角色死亡，不能制作

            if (character.Attributes.GetAttributeValue(AttributeType.Energy) < craft_energy)
                return false; // 如果能量不足，不能制作

            bool has_craft_cost = HasCraftCost(item); // 是否有制作所需物品
            bool has_pay_slot = HasCraftPaySlot(item, pay_slot); // 是否提供了支付槽
            bool has_near = skip_near || HasCraftNear(item); // 是否在所需的建造附近
            bool has_cost = has_craft_cost || has_pay_slot; // 是否有支付物品
            return has_near && has_cost; // 如果满足条件，返回true
        }

        // 判断支付槽是否满足制作要求
        public bool HasCraftPaySlot(CraftData item, InventorySlot pay_slot)
        {
            if (pay_slot != null)
            {
                ItemData idata = pay_slot.GetItem();
                return idata != null && idata.GetBuildData() == item; // 如果支付槽中有对应的物品，返回true
            }
            return false;
        }

        // 判断是否有足够的物品进行制作
        public bool HasCraftCost(CraftData item)
        {
            bool can_craft = true;
            CraftCostData cost = item.GetCraftCost(); // 获取制作所需的物品和要求
            Dictionary<GroupData, int> item_groups = new Dictionary<GroupData, int>(); // 存储物品的分组数据，避免重复计算

            foreach (KeyValuePair<ItemData, int> pair in cost.craft_items)
            {
                AddCraftCostItemsGroups(item_groups, pair.Key, pair.Value); // 添加物品到分组
                if (!character.Inventory.HasItem(pair.Key, pair.Value)) // 检查角色是否有足够的物品
                    can_craft = false;
            }

            foreach (KeyValuePair<GroupData, int> pair in cost.craft_fillers)
            {
                int value = pair.Value + CountCraftCostGroup(item_groups, pair.Key); // 计算分组物品的总数量
                if (!character.Inventory.HasItemInGroup(pair.Key, value)) // 检查角色是否有足够的物品组
                    can_craft = false;
            }

            foreach (KeyValuePair<CraftData, int> pair in cost.craft_requirements)
            {
                if (CountRequirements(pair.Key) < pair.Value) // 检查角色是否有足够的建筑或要求物品
                    can_craft = false;
            }
            return can_craft;
        }

        // 判断角色是否在制作所需的建造物附近
        public bool HasCraftNear(CraftData item)
        {
            bool can_craft = true;
            CraftCostData cost = item.GetCraftCost();
            if (cost.craft_near != null && !character.IsNearGroup(cost.craft_near) && !character.EquipData.HasItemInGroup(cost.craft_near))
                can_craft = false; // 如果角色不在所需的建造物附近，返回false
            return can_craft;
        }

        // 添加制作物品的分组信息
        private void AddCraftCostItemsGroups(Dictionary<GroupData, int> item_groups, ItemData item, int quantity)
        {
            foreach (GroupData group in item.groups)
            {
                if (item_groups.ContainsKey(group))
                    item_groups[group] += quantity;
                else
                    item_groups[group] = quantity;
            }
        }

        // 计算指定分组的物品数量
        private int CountCraftCostGroup(Dictionary<GroupData, int> item_groups, GroupData group)
        {
            if (item_groups.ContainsKey(group))
                return item_groups[group];
            return 0;
        }

        // 支付制作费用
        public void PayCraftingCost(CraftData item, bool build = false)
        {
            CraftCostData cost = item.GetCraftCost();
            foreach (KeyValuePair<ItemData, int> pair in cost.craft_items)
            {
                character.Inventory.UseItem(pair.Key, pair.Value); // 从物品栏扣除制作物品
            }
            foreach (KeyValuePair<GroupData, int> pair in cost.craft_fillers)
            {
                character.Inventory.UseItemInGroup(pair.Key, pair.Value); // 从物品栏扣除分组物品
            }

            float cost_energy = build ? build_energy : craft_energy; // 根据是否建造确定消耗的能量
            character.Attributes.AddAttribute(AttributeType.Energy, -cost_energy); // 扣除能量
        }

        // 支付带支付槽的制作费用
        public void PayCraftingCost(CraftData item, InventorySlot pay_slot, bool build = false)
        {
            if (pay_slot != null && pay_slot.inventory != null)
            {
                InventoryData inventory = pay_slot.inventory;
                inventory.RemoveItemAt(pay_slot.slot, 1); // 从支付槽移除物品
            }
            else
            {
                PayCraftingCost(item, build); // 如果没有支付槽，直接支付制作费用
            }
        }

        // 计算要求物品的数量
        public int CountRequirements(CraftData requirement)
        {
            if (requirement is ItemData)
                return character.Inventory.CountItem((ItemData)requirement); // 如果是物品类型，返回角色物品数量
            else
                return CraftData.CountSceneObjects(requirement); // 如果是场景对象，返回场景中的物品数量
        }

        // ---- 制作过程 ----

        // 开始制作或建造
        public void StartCraftingOrBuilding(CraftData data)
        {
            if (CanCraft(data)) // 如果可以制作
            {
                ConstructionData construct = data.GetConstruction();
                PlantData plant = data.GetPlant();

                if (construct != null)
                    CraftConstructionBuildMode(construct); // 开始建造
                else if (plant != null)
                    CraftPlantBuildMode(plant, 0); // 开始种植
                else
                    StartCrafting(data); // 开始制作

                TheAudio.Get().PlaySFX("craft", data.craft_sound); // 播放制作声音
            }
        }

        // 开始制作并启动计时器
        public void StartCrafting(CraftData data)
        {
            if (data != null && current_crafting == null)
            {
                current_crafting = data;
                craft_timer = 0f; // 重置计时器
                character.StopMove(); // 停止角色移动

                // 如果有进度条，创建并显示
                if (AssetData.Get().action_progress != null && data.craft_duration > 0.1f)
                {
                    craft_progress = Instantiate(AssetData.Get().action_progress, transform);
                    craft_progress.GetComponent<ActionProgress>().duration = data.craft_duration;
                }

                // 如果制作时间极短，立即完成
                if (data.craft_duration < 0.01f)
                    CompleteCrafting();
            }
        }

        // 在到达建造位置后，开始制作（持续时间/动画）
        public void StartCraftBuilding()
        {
            if (current_buildable != null)
            {
                StartCraftBuilding(current_buildable.transform.position); // 使用当前建造位置
            }
        }


        // 开始建造功能，传入建造目标位置
        public void StartCraftBuilding(Vector3 pos)
        {
            // 确保当前建造数据和物品有效，并且没有正在进行的制作
            if (current_build_data != null && current_buildable != null && current_crafting == null)
            {
                // 检查是否能支付建造费用
                if (CanCraft(current_build_data, build_pay_slot, true))
                {
                    // 设置临时建造位置来进行建造位置验证
                    current_buildable.SetBuildPositionTemporary(pos);
                    
                    // 如果能建造，则设置最终建造位置，开始制作
                    if (current_buildable.CheckIfCanBuild())
                    {
                        current_buildable.SetBuildPosition(pos);
                        StartCrafting(current_build_data); // 开始制作
                        character.FaceTorward(pos); // 角色面向建造位置
                    }
                }
            }
        }

        // ------------ 建造物品功能 ----------

        // 启动建造模式，传入物品所在槽位的库存
        public void BuildItemBuildMode(InventoryData inventory, int slot)
        {
            ItemData idata = inventory.GetItem(slot);
            if (idata != null)
            {
                CraftData cdata = idata.GetBuildData(); // 获取物品的建造数据
                if (cdata != null)
                {
                    CraftBuildMode(cdata); // 进入建造模式
                    build_pay_slot = new InventorySlot(inventory, slot); // 保存物品槽位信息
                }
            }
        }

        // 开始建造物品，检查库存并执行建造
        public void BuildItem(InventoryData inventory, int slot)
        {
            InventoryItemData invdata = inventory?.GetInventoryItem(slot);
            ItemData idata = ItemData.Get(invdata?.item_id);
            
            // 检查物品和数据有效性
            if (invdata == null || idata == null)
                return;

            CraftData data = null;

            // 判断物品类型并获取相关建造数据
            if (idata.construction_data != null)
                data = idata.construction_data;
            else if (idata.plant_data != null)
                data = idata.plant_data;
            else if (idata.character_data != null)
                data = idata.character_data;

            // 检查是否可以建造并执行建造
            if (data != null && CanCraft(data, true))
            {
                inventory.RemoveItemAt(slot, 1); // 移除库存中的物品

                // 根据建造数据创建相应的物品
                Craftable craftable = CraftCraftable(data, true);
                if (craftable != null && craftable is Construction)
                {
                    Construction construction = (Construction)craftable;
                    BuiltConstructionData constru = PlayerData.Get().GetConstructed(construction.GetUID());
                    if (idata.HasDurability())
                        constru.durability = invdata.durability; // 保存耐久度
                }

                TheAudio.Get().PlaySFX("craft", idata.craft_sound); // 播放建造声音
                PlayerUI.Get(character.player_id)?.CancelSelection(); // 取消当前选择
            }
        }

        // ----- 建造模式中的制作 -----

        // 根据制作数据进入建造模式
        public void CraftBuildMode(CraftData data)
        {
            if (data is PlantData)
                CraftPlantBuildMode((PlantData)data, 0); // 进入种植模式
            if (data is ConstructionData)
                CraftConstructionBuildMode((ConstructionData)data); // 进入建筑模式
            if (data is CharacterData)
                CraftCharacterBuildMode((CharacterData)data); // 进入角色建造模式
        }

        // 进入种植模式
        public void CraftPlantBuildMode(PlantData plant, int stage)
        {
            CancelCrafting(); // 取消当前制作

            Plant aplant = Plant.CreateBuildMode(plant, transform.position, stage); // 创建种植物体
            current_buildable = aplant.GetBuildable(); // 获取可建造物体
            current_buildable.StartBuild(character); // 开始建造
            current_build_data = plant; // 保存当前建造数据
            build_pay_slot = null; // 清空支付槽
            clicked_build = false; // 重置点击状态
            build_timer = 0f; // 重置建造计时器
        }

        // 进入建筑模式
        public void CraftConstructionBuildMode(ConstructionData item)
        {
            CancelCrafting(); // 取消当前制作

            Construction construction = Construction.CreateBuildMode(item, transform.position + transform.forward * 1f); // 创建建筑
            current_buildable = construction.GetBuildable(); // 获取可建造物体
            current_buildable.StartBuild(character); // 开始建造
            current_build_data = item; // 保存当前建造数据
            build_pay_slot = null; // 清空支付槽
            clicked_build = false; // 重置点击状态
            build_timer = 0f; // 重置建造计时器
        }

        // 进入角色建造模式
        public void CraftCharacterBuildMode(CharacterData item)
        {
            CancelCrafting(); // 取消当前制作

            Character acharacter = Character.CreateBuildMode(item, transform.position + transform.forward * 1f); // 创建角色
            current_buildable = acharacter.GetBuildable(); // 获取可建造物体
            if (current_buildable != null)
                current_buildable.StartBuild(character); // 开始建造
            current_build_data = item; // 保存当前建造数据
            build_pay_slot = null; // 清空支付槽
            clicked_build = false; // 重置点击状态
            build_timer = 0f; // 重置建造计时器
        }

        // ----- 取消和确认 -----

        // 取消当前制作
        public void CancelCrafting()
        {
            current_crafting = null; // 清空当前制作数据
            if (craft_progress != null)
                Destroy(craft_progress); // 销毁制作进度条
            CancelBuilding(); // 取消建造
        }

        // 取消当前建造
        public void CancelBuilding()
        {
            if (current_buildable != null)
            {
                Destroy(current_buildable.gameObject); // 销毁当前建造物体
                current_buildable = null;
                current_build_data = null;
                build_pay_slot = null;
                clicked_build = false;
            }
        }

        // 尝试在指定位置建造
        public void TryBuildAt(Vector3 pos)
        {
            bool in_range = character.interact_type == PlayerInteractBehavior.MoveAndInteract || IsInBuildRange();
            if (!in_range)
                return;

            if (!clicked_build && current_buildable != null)
            {
                current_buildable.SetBuildPositionTemporary(pos); // 设置临时建造位置

                bool can_build = current_buildable.CheckIfCanBuild(); // 检查是否可以建造
                if (can_build)
                {
                    current_buildable.SetBuildPosition(pos);
                    clicked_build = true; // 设置为已点击建造
                    character.MoveTo(pos); // 角色移动到目标位置
                }
            }
        }

        // ----- 制作完成 -----

        // 完成制作计时
        public void CompleteCrafting()
        {
            if (current_crafting != null)
            {
                if (current_buildable != null)
                    CompleteBuilding(current_buildable.transform.position); // 如果有建造物体，完成建造
                else
                    CraftCraftable(current_crafting); // 否则完成制作

                current_crafting = null;
            }
        }

        // 立即制作
        public Craftable CraftCraftable(CraftData data, bool skip_cost = false)
        {
            ItemData item = data.GetItem();
            ConstructionData construct = data.GetConstruction();
            PlantData plant = data.GetPlant();
            CharacterData character = data.GetCharacter();

            // 根据数据类型执行对应的制作操作
            if (item != null)
                return CraftItem(item, skip_cost);
            else if (construct != null)
                return CraftConstruction(construct, skip_cost);
            else if (plant != null)
                return CraftPlant(plant, 0, skip_cost);
            else if (character != null)
                return CraftCharacter(character, skip_cost);
            return null;
        }

        // 制作物品
        public Item CraftItem(ItemData item, bool skip_cost = false)
        {
            if (CanCraft(item, skip_cost))
            {
                if (!skip_cost)
                    PayCraftingCost(item); // 支付制作费用

                Item ritem = null;
                // 检查是否有足够的物品空间来接收制作物品
                if (character.Inventory.CanTakeItem(item, item.craft_quantity))
                    character.Inventory.GainItem(item, item.craft_quantity); // 获取物品
                else
                    ritem = Item.Create(item, transform.position, item.craft_quantity); // 创建物品

                character.SaveData.AddCraftCount(item.id); // 保存制作计数
                character.Attributes.GainXP(item.craft_xp_type, item.craft_xp); // 增加经验

                if (onCraft != null)
                    onCraft.Invoke(item); // 调用制作完成的回调事件

                return ritem;
            }
            return null;
        }

        // 制作角色
        public Character CraftCharacter(CharacterData character, bool skip_cost = false)
        {
            if (CanCraft(character, skip_cost))
            {
                if (!skip_cost)
                    PayCraftingCost(character); // 支付制作费用

                Vector3 pos = transform.position + transform.forward * 0.8f;
                Character acharacter = Character.Create(character, pos); // 创建角色

                this.character.SaveData.AddCraftCount(character.id); // 保存制作计数
                this.character.Attributes.GainXP(character.craft_xp_type, character.craft_xp); // 增加经验

                if (onCraft != null)
                    onCraft.Invoke(character); // 调用制作完成的回调事件

                return acharacter;
            }
            return null;
        }

        // 制作植物
        public Plant CraftPlant(PlantData plant, int stage, bool skip_cost = false)
        {
            if (CanCraft(plant, skip_cost))
            {
                if (!skip_cost)
                    PayCraftingCost(plant); // 支付制作费用

                Vector3 pos = transform.position + transform.forward * 0.4f;
                Plant aplant = Plant.Create(plant, pos, stage); // 创建植物

                character.SaveData.AddCraftCount(plant.id); // 保存制作计数
                character.Attributes.GainXP(plant.craft_xp_type, plant.craft_xp); // 增加经验

                if (onCraft != null)
                    onCraft.Invoke(plant); // 调用制作完成的回调事件

                return aplant;
            }
            return null;
        }

        // 制作建筑
        public Construction CraftConstruction(ConstructionData construct, bool skip_cost = false)
        {
            if (CanCraft(construct, skip_cost))
            {
                if (!skip_cost)
                    PayCraftingCost(construct); // 支付制作费用

                Vector3 pos = transform.position + transform.forward * 1f;
                Construction aconstruct = Construction.Create(construct, pos); // 创建建筑

                character.SaveData.AddCraftCount(construct.id); // 保存制作计数
                character.Attributes.GainXP(construct.craft_xp_type, construct.craft_xp); // 增加经验

                if (onCraft != null)
                    onCraft.Invoke(construct); // 调用制作完成的回调事件

                return aconstruct;
            }
            return null;
        }

        // 完成建造
        public void CompleteBuilding()
        {
            if (current_buildable != null)
            {
                CompleteBuilding(current_buildable.transform.position); // 完成建造
            }
        }

        // 完成建造并支付建造费用
        public void CompleteBuilding(Vector3 pos)
        {
            CraftData item = current_crafting;
            if (item != null && current_buildable != null && CanCraft(item, build_pay_slot, true))
            {
                current_buildable.SetBuildPositionTemporary(pos); // 设置临时建造位置

                if (current_buildable.CheckIfCanBuild())
                {
                    current_buildable.SetBuildPosition(pos); // 设置最终建造位置

                    character.FaceTorward(pos); // 角色面向建造位置

                    PayCraftingCost(item, build_pay_slot, true); // 支付建造费用

                    Buildable buildable = current_buildable;
                    buildable.FinishBuild(); // 完成建造

                    character.SaveData.AddCraftCount(item.id); // 保存制作计数
                    character.Attributes.GainXP(item.craft_xp_type, item.craft_xp); // 增加经验

                    current_buildable = null;
                    current_build_data = null;
                    clicked_build = false;
                    character.StopAutoMove(); // 停止自动移动

                    PlayerUI.Get(character.player_id)?.CancelSelection(); // 取消选择
                    TheAudio.Get().PlaySFX("craft", buildable.build_audio); // 播放建造音效

                    if (onBuild != null)
                        onBuild.Invoke(buildable); // 调用建造完成的回调事件

                    character.TriggerBusy(1f); // 角色忙碌
                }
            }
        }

        // ---- 获取相关数据 ----

        // 学习某个制作图纸
        public void LearnCraft(string craft_id)
        {
            character.SaveData.UnlockID(craft_id);
        }

        // 检查是否已经学会某个制作图纸
        public bool HasLearnt(string craft_id)
        {
            return character.SaveData.IsIDUnlocked(craft_id);
        }

        // 获取某个物品的总制作次数
        public int CountTotalCrafted(CraftData craftable)
        {
            if (craftable != null)
                return character.SaveData.GetCraftCount(craftable.id);
            return 0;
        }

        // 重置某个物品的制作次数
        public void ResetCraftCount(CraftData craftable)
        {
            if (craftable != null)
                character.SaveData.ResetCraftCount(craftable.id);
        }

        // 重置所有物品的制作次数
        public void ResetCraftCount()
        {
            character.SaveData.ResetCraftCount();
        }

        // 判断是否点击建造按钮
        public bool ClickedBuild()
        {
            return clicked_build;
        }

        // 判断是否处于建造状态
        public bool CanBuild()
        {
            return current_buildable != null && current_buildable.IsBuilding() && build_timer > 0.5f;
        }

        // 判断是否在建造范围内
        public bool IsInBuildRange()
        {
            if (current_buildable == null)
                return false;
            Vector3 dist = (character.GetInteractCenter() - current_buildable.transform.position);
            return dist.magnitude < current_buildable.GetBuildRange(character);
        }

        // 判断是否处于建造模式
        public bool IsBuildMode()
        {
            return current_buildable != null && current_buildable.IsBuilding();
        }

        // 判断是否正在制作
        public bool IsCrafting()
        {
            return current_crafting != null;
        }

        // 获取当前制作进度
        public float GetCraftProgress()
        {
            if (current_crafting != null && current_crafting.craft_duration > 0.01f)
                return craft_timer / current_crafting.craft_duration; // 计算并返回制作进度
            return 0f;
        }

        // 获取当前建造物体
        public Buildable GetCurrentBuildable()
        {
            return current_buildable; // 如果没有建造，则返回null
        }

        // 获取当前正在制作的物品数据
        public CraftData GetCurrentCrafting()
        {
            return current_crafting;
        }

        // 获取角色数据
        public PlayerCharacter GetCharacter()
        {
            return character;
        }

        // 获取最近的制作站
        public CraftStation GetCraftStation()
        {
            CraftStation station = CraftStation.GetNearestInRange(transform.position);
            return station;
        }

        // 获取当前制作站的所有制作组
        public List<GroupData> GetCraftGroups()
        {
            CraftStation station = CraftStation.GetNearestInRange(transform.position);
            if (station != null)
                return new List<GroupData>(station.craft_groups);
            else
                return new List<GroupData>(craft_groups);
        }

    }
}
