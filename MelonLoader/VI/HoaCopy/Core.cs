// --- START OF FILE Core.cs --- Của mod HoaCopy (Đã chỉnh sửa)

using CustomizeLib;
using CustomizeLib.MelonLoader;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.Injection;
using Il2CppTMPro;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[assembly: MelonInfo(typeof(HoaCopy.Core), "PvzRhTomiSakaeMods v1.1 - HoaCopy", "1.1.0", "TomiSakae", null)]
[assembly: MelonGame("LanPiaoPiao", "PlantsVsZombiesRH")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]

namespace HoaCopy
{
    [RegisterTypeInIl2Cpp]
    public class HoaCopyComponent : MonoBehaviour
    {
        public PlantType consumedPlantType = PlantType.Nothing;
        public Producer plantComponent = null;
        public bool isReadyToProduceCard = false;

        void Awake()
        {
            plantComponent = GetComponent<Producer>();
            if (plantComponent == null)
            {
                MelonLogger.Error("[HoaCopy] Không tìm thấy component Producer trên HoaCopyComponent!");
            }
            isReadyToProduceCard = false;
            consumedPlantType = PlantType.Nothing;
        }

        public void ResetConsumedState()
        {
            consumedPlantType = PlantType.Nothing;
            isReadyToProduceCard = false;
        }

        public void ConsumePlant(PlantType typeToConsume)
        {
            PlantType previousType = consumedPlantType;
            consumedPlantType = typeToConsume;
            isReadyToProduceCard = true;
            if (previousType != PlantType.Nothing && previousType != typeToConsume)
            {
                MelonLogger.Msg($"[HoaCopy ID:{plantComponent?.GetInstanceID()}] Đã ĐỔI cây tiêu thụ từ {previousType} thành {typeToConsume}. Sẵn sàng tạo thẻ mới.");
            }
            else if (previousType == PlantType.Nothing)
            {
                MelonLogger.Msg($"[HoaCopy ID:{plantComponent?.GetInstanceID()}] Đã tiêu thụ cây: {typeToConsume}. Sẵn sàng tạo thẻ.");
            }
            else
            {
                MelonLogger.Msg($"[HoaCopy ID:{plantComponent?.GetInstanceID()}] Tiêu thụ lại chính nó: {typeToConsume}. Tiếp tục tạo thẻ.");
            }
        }
    }

    public class Core : MelonMod
    {
        public const int HoaCopyPlantId = 2036;
        internal static GameObject droppedCardPrefab = null;
        public const float HoaCopyCooldown = 200f;

        public override void OnInitializeMelon()
        {
            var ab = CustomCore.GetAssetBundle(MelonAssembly.Assembly, "hoacopy");

            // Đăng ký cây tùy chỉnh bằng CustomizeLib
            CustomCore.RegisterCustomPlant<Producer, HoaCopyComponent>(
                id: HoaCopyPlantId,
                prefab: ab.GetAsset<GameObject>("SunflowerPrefab"),
                preview: ab.GetAsset<GameObject>("SunflowerPreview"),
                fusions: new List<ValueTuple<int, int>> { (1, 245), (245, 1) }, // Sunflower (1) + Jalapeno (16)
                attackInterval: 0f,
                produceInterval: 15f, // Thời gian sản xuất thẻ
                attackDamage: 0,
                maxHealth: 300,
                cd: HoaCopyCooldown,
                sun: 150
            );

            string plantName = "Hướng Dương Sao Chép";
            string plantDescription =
                "Sao chép cây trồng lên nó.\n" +
                "Sản lượng: <color=red>1 thẻ bài/15 giây (nếu có cây sao chép)</color>\n" +
                $"Thời gian hồi: <color=red>{HoaCopyCooldown} giây</color>\n" +
                "Công thức: <color=red>Hoa Hướng Dương + Ớt</color>\n\n" +
                "Loài hướng dương kỳ lạ này có khả năng phân tích và tái tạo cấu trúc của các loài thực vật khác khi chúng được đặt lên trên. Sau một thời gian, nó sẽ tạo ra một hạt giống của cây đó.";

            CustomCore.AddPlantAlmanacStrings(HoaCopyPlantId, plantName, plantDescription);

            // Đã xóa các dòng gọi ModPlantUISystem
            MelonLogger.Msg($"[HoaCopy] Đã khởi tạo và đăng ký với CD {HoaCopyCooldown} giây.");
        }

        [HarmonyPatch(typeof(GameAPP), "LoadResources")]
        public static class GameAPP_LoadResources_Patch
        {
            public static void Postfix()
            {
                if (Core.droppedCardPrefab == null)
                {
                    try
                    {
                        string prefabName = "DroppedCard";
                        Core.droppedCardPrefab = GameAPP.itemPrefab?.FirstOrDefault(go => go != null && go.name == prefabName);

                        if (Core.droppedCardPrefab != null)
                        {
                            MelonLogger.Msg($"[HoaCopy] Đã tìm thấy và lưu DroppedCard prefab: {Core.droppedCardPrefab.name}");
                        }
                        else
                        {
                            MelonLogger.Error($"[HoaCopy] Không tìm thấy prefab '{prefabName}' trong GameAPP.itemPrefab.");
                        }
                    }
                    catch (Exception ex) { MelonLogger.Error($"[HoaCopy] Lỗi khi tìm DroppedCard prefab: {ex}"); }
                }
            }
        }

        [HarmonyPatch(typeof(CreatePlant), "SetPlant")]
        public static class CreatePlant_SetPlant_Patch
        {
            // Chữ ký của hàm Prefix cần khớp với một trong các overload của SetPlant
            public static bool Prefix(int newColumn, int newRow, PlantType theSeedType, ref GameObject __result, bool isFreeSet, bool withEffect, Plant hitplant)
            {
                if (Board.Instance == null || theSeedType == PlantType.Nothing)
                {
                    return true;
                }

                Plant existingPlant = Lawnf.GetPlant(newColumn, newRow, Board.Instance);

                if (existingPlant != null && existingPlant.thePlantType == (PlantType)HoaCopyPlantId)
                {
                    var hoaCopyComponent = existingPlant.GetComponent<HoaCopyComponent>();
                    if (hoaCopyComponent != null)
                    {
                        // Để game tự trừ tiền, không cần xử lý ở đây
                        hoaCopyComponent.ConsumePlant(theSeedType);

                        if (withEffect && CreatePlant.Instance != null)
                        {
                            Vector3 plantWorldPosition = existingPlant.transform.position;
                            CreatePlant.Instance.CreatePlantParticle(newColumn, newRow, plantWorldPosition);
                        }

                        // Trả về cây HoaCopy hiện có và ngăn hàm gốc trồng cây mới
                        __result = existingPlant.gameObject;
                        return false;
                    }
                    else
                    {
                        MelonLogger.Error($"[HoaCopy] Cây HoaCopy tại ({newColumn},{newRow}) thiếu component HoaCopyComponent!");
                        return true;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Producer), "ProduceSun")]
        public static class Producer_ProduceSun_Patch
        {
            public static void Postfix(Producer __instance)
            {
                if (__instance == null || __instance.thePlantType != (PlantType)Core.HoaCopyPlantId) return;

                var hoaCopyComponent = __instance.GetComponent<HoaCopyComponent>();

                if (hoaCopyComponent != null && hoaCopyComponent.isReadyToProduceCard && hoaCopyComponent.consumedPlantType != PlantType.Nothing)
                {
                    if (Core.droppedCardPrefab == null)
                    {
                        MelonLogger.Error("[HoaCopy] ProduceSun: DroppedCard prefab là null! Không thể tạo thẻ rơi.");
                        return;
                    }

                    PlantType consumedType = hoaCopyComponent.consumedPlantType;
                    var plantData = PlantDataLoader.plantData[(int)consumedType];
                    if (plantData == null) { MelonLogger.Error($"[HoaCopy] Không tìm thấy PlantData cho {consumedType}."); return; }

                    int cost = 0; // Thẻ rơi miễn phí
                    float cd = plantData.field_Public_Single_2;

                    Transform cardParentTransform = GameAPP.canvasUp;
                    if (cardParentTransform == null)
                    {
                        MelonLogger.Error("[HoaCopy] Không tìm thấy GameAPP.canvasUp! Không thể tạo thẻ rơi.");
                        return;
                    }

                    GameObject droppedCardGO = UnityEngine.Object.Instantiate(Core.droppedCardPrefab, cardParentTransform);
                    if (droppedCardGO == null) { MelonLogger.Error("[HoaCopy] Instantiate DroppedCard prefab thất bại."); return; }
                    Vector3 startPos = __instance.transform.position + new Vector3(UnityEngine.Random.Range(-0.1f, 0.1f), UnityEngine.Random.Range(0.1f, 0.3f), 0);
                    droppedCardGO.transform.position = startPos;

                    RectTransform rect = droppedCardGO.GetComponent<RectTransform>();
                    if (rect != null) { rect.localScale = Vector3.one; }
                    else { MelonLogger.Warning($"[HoaCopy] DroppedCard {consumedType} thiếu RectTransform?"); }

                    DroppedCard droppedCard = droppedCardGO.GetComponent<DroppedCard>();
                    if (droppedCard == null) { MelonLogger.Error("[HoaCopy] Prefab DroppedCard thiếu component DroppedCard."); UnityEngine.Object.Destroy(droppedCardGO); return; }

                    droppedCard.thePlantType = consumedType;
                    droppedCard.theSeedCost = cost;
                    droppedCard.fullCD = cd;
                    droppedCard.CD = cd;
                    droppedCard.isAvailable = true;
                    droppedCard.isPickUp = false;
                    droppedCard.movingWay = 0;

                    try { Lawnf.ChangeCardSprite(consumedType, droppedCardGO); }
                    catch (Exception ex) { MelonLogger.Error($"[HoaCopy] Lỗi khi gọi Lawnf.ChangeCardSprite: {ex}"); }

                    TextMeshProUGUI costText = droppedCardGO.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (costText != null) { costText.text = cost.ToString(); }

                    Collider2D cardCollider = droppedCardGO.GetComponent<Collider2D>();
                    if (cardCollider != null) { cardCollider.enabled = true; }
                    else
                    {
                        cardCollider = droppedCardGO.GetComponentInChildren<Collider2D>(true);
                        if (cardCollider != null) { cardCollider.enabled = true; }
                        else { MelonLogger.Warning($"[HoaCopy] Không tìm thấy Collider2D trên DroppedCard {consumedType} hoặc con của nó."); }
                    }

                    droppedCardGO.SetActive(true);
                }
            }
        }

        [HarmonyPatch(typeof(CardUI), "OnMouseDown")]
        public static class CardUI_OnMouseDown_Patch
        {
            public static void Postfix(CardUI __instance)
            {
                if (__instance.TryCast<DroppedCard>() != null && !__instance.isPickUp && __instance.isAvailable && __instance.CD >= __instance.fullCD)
                {
                    if (Mouse.Instance != null && Mouse.Instance.mouseItemType == MouseItemType.Nothing)
                    {
                        if (!__instance.PickUp())
                        {
                            MelonLogger.Warning($"[HoaCopy Patch OnMouseDown] Gọi PickUp() cho {__instance.thePlantType} trả về false.");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CardUI), "Start")]
        public static class CardUI_Start_Patch
        {
            public static void Postfix(CardUI __instance)
            {
                try
                {
                    if (__instance.thePlantType == (PlantType)Core.HoaCopyPlantId)
                    {
                        if (__instance.TryCast<DroppedCard>() == null) // Chỉ áp dụng cho thẻ gốc trong khay
                        {
                            __instance.fullCD = Core.HoaCopyCooldown;
                            __instance.CD = 0f; // Bắt đầu hồi chiêu từ 0
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[HoaCopy Patch Start] Lỗi: {ex}");
                }
            }
        }
    }
}
// --- END OF FILE Core.cs ---