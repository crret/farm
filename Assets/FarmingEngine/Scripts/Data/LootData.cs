using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarmingEngine
{
    /// <summary>
    /// 战利品数据，用于定义掉落的物品及其概率。
    /// </summary>
    [CreateAssetMenu(fileName ="LootData", menuName ="FarmingEngine/LootData", order =20)]
    public class LootData : SData
    {
        public ItemData item;        // 掉落的物品数据
        public int quantity = 1;     // 掉落数量，默认为1
        public float probability = 1f;   // 掉落的概率，1f = 100%，0.5f = 50%，0f = 0%
    }

}