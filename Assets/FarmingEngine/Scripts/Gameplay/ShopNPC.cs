using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarmingEngine {

    [RequireComponent(typeof(Selectable))]  // 需要挂载Selectable组件
    public class ShopNPC : MonoBehaviour
    {
        public string title;  // 商店标题

        [Header("Buy")]  // 购买项
        public ItemData[] items;  // 购买物品列表

        [Header("Sell")]  // 出售项
        public GroupData sell_group;  // 出售物品的群组，如果为null，则可以出售任何物品

        private Selectable selectable;  // Selectable组件的引用

        private void Awake()
        {
            selectable = GetComponent<Selectable>();  // 获取Selectable组件的引用
        }

        // 打开商店
        public void OpenShop()
        {
            // 获取最近的玩家角色
            PlayerCharacter character = PlayerCharacter.GetNearest(transform.position);
            if (character != null)
                OpenShop(character);
        }

        // 打开商店给特定的玩家角色
        public void OpenShop(PlayerCharacter player)
        {
            List<ItemData> buy_items = new List<ItemData>(items);  // 创建购买物品列表的副本
            ShopPanel.Get().ShowShop(player, title, buy_items, sell_group);  // 显示商店界面
        }
    }

}