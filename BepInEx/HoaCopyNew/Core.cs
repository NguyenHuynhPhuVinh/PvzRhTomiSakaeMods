// --- START OF FILE Core.cs --- Của mod HoaCopy (Phiên bản BepInEx - Sửa theo mẫu NullNut)

using BepInEx;
using BepInEx.Unity.IL2CPP;
using CustomizeLib.BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
// Lưu ý: Đã xóa các using không cần thiết như Il2Cpp và Il2CppTMPro

namespace HoaCopy
{
    // THAY ĐỔI: Đã xóa attribute [RegisterTypeInIl2Cpp]
    public class HoaCopyComponent : MonoBehaviour
    {
        // THAY ĐỔI: Thêm 2 constructor giống hệt mẫu NullNut
        public HoaCopyComponent() : base(ClassInjector.DerivedConstructorPointer<HoaCopyComponent>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }
        public HoaCopyComponent(IntPtr i) : base(i) { }

        public PlantType consumedPlantType = PlantType.Nothing;
        public Producer plantComponent = null;
        public bool isReadyToProduceCard = false;

        void Awake()
        {
            plantComponent = GetComponent<Producer>();
            if (plantComponent == null)
            {
                Core.Instance.Log.LogError("[HoaCopy] Không tìm thấy component Producer trên HoaCopyComponent!");
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
                Core.Instance.Log.LogInfo($"[HoaCopy ID:{plantComponent?.GetInstanceID()}] Đã ĐỔI cây tiêu thụ từ {previousType} thành {typeToConsume}. Sẵn sàng tạo thẻ mới.");
            }
            else if (previousType == PlantType.Nothing)
            {
                Core.Instance.Log.LogInfo($"[HoaCopy ID:{plantComponent?.GetInstanceID()}] Đã tiêu thụ cây: {typeToConsume}. Sẵn sàng tạo thẻ.");
            }
            else
            {
                Core.Instance.Log.LogInfo($"[HoaCopy ID:{plantComponent?.GetInstanceID()}] Tiêu thụ lại chính nó: {typeToConsume}. Tiếp tục tạo thẻ.");
            }
        }
    }

    [BepInPlugin(Core.PluginGUID, Core.PluginName, Core.PluginVersion)]
    public class Core : BasePlugin
    {
        public const string PluginGUID = "com.tomisakae.hoacopy";
        public const string PluginName = "PvzRhTomiSakaeMods v1.2 - HoaCopy (BepInEx)";
        public const string PluginVersion = "1.2.0";

        public static Core Instance;

        public const int HoaCopyPlantId = 2036;
        internal static GameObject droppedCardPrefab = null;

        public override void Load()
        {
            Instance = this;

            // THAY ĐỔI: Đăng ký component thủ công giống hệt mẫu NullNut
            ClassInjector.RegisterTypeInIl2Cpp<HoaCopyComponent>();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);

            var ab = CustomCore.GetAssetBundle(Assembly.GetExecutingAssembly(), "hoacopy");

            CustomCore.RegisterCustomPlant<Producer, HoaCopyComponent>(
                HoaCopyPlantId,
                ab.GetAsset<GameObject>("SunflowerPrefab"),
                ab.GetAsset<GameObject>("SunflowerPreview"),
                new List<ValueTuple<int, int>> { (1, 245), (245, 1) },
                0f, 15f, 0, 300, 0f, 150
            );

            string plantName = "Hướng Dương Sao Chép";
            string plantDescription =
                "Sao chép cây trồng lên nó.\n" +
                "Sản lượng: <color=red>1 thẻ bài/15 giây (nếu có cây sao chép)</color>\n" +
                "Công thức: <color=red>Hoa Hướng Dương + Ớt</color>\n\n" +
                "Loài hướng dương kỳ lạ này có khả năng phân tích và tái tạo cấu trúc của các loài thực vật khác khi chúng được đặt lên trên. Sau một thời gian, nó sẽ tạo ra một hạt giống của cây đó.";

            CustomCore.AddPlantAlmanacStrings(HoaCopyPlantId, plantName, plantDescription);

            Log.LogInfo($"[HoaCopy] Đã khởi tạo và đăng ký. (BepInEx)");
        }

        #region Harmony Patches
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
                            Instance.Log.LogInfo($"[HoaCopy] Đã tìm thấy và lưu DroppedCard prefab: {Core.droppedCardPrefab.name}");
                        }
                        else
                        {
                            Instance.Log.LogError($"[HoaCopy] Không tìm thấy prefab '{prefabName}' trong GameAPP.itemPrefab.");
                        }
                    }
                    catch (Exception ex) { Instance.Log.LogError($"[HoaCopy] Lỗi khi tìm DroppedCard prefab: {ex}"); }
                }
            }
        }

        [HarmonyPatch(typeof(CreatePlant), nameof(CreatePlant.SetPlant))]
        public static class CreatePlant_SetPlant_Patch
        {
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
                        hoaCopyComponent.ConsumePlant(theSeedType);

                        if (withEffect && CreatePlant.Instance != null)
                        {
                            Vector3 plantWorldPosition = existingPlant.transform.position;
                            CreatePlant.Instance.CreatePlantParticle(newColumn, newRow, plantWorldPosition);
                        }

                        __result = existingPlant.gameObject;
                        return false;
                    }
                    else
                    {
                        Instance.Log.LogError($"[HoaCopy] Cây HoaCopy tại ({newColumn},{newRow}) thiếu component HoaCopyComponent!");
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
                    if (Core.droppedCardPrefab == null) { Instance.Log.LogError("[HoaCopy] DroppedCard prefab là null!"); return; }

                    PlantType consumedType = hoaCopyComponent.consumedPlantType;
                    var plantData = PlantDataLoader.plantData[(int)consumedType];
                    if (plantData == null) { Instance.Log.LogError($"[HoaCopy] Không tìm thấy PlantData cho {consumedType}."); return; }

                    int cost = 0;
                    Transform cardParentTransform = GameAPP.canvasUp;
                    if (cardParentTransform == null) { Instance.Log.LogError("[HoaCopy] Không tìm thấy GameAPP.canvasUp!"); return; }

                    GameObject droppedCardGO = UnityEngine.Object.Instantiate(Core.droppedCardPrefab, cardParentTransform);
                    if (droppedCardGO == null) { Instance.Log.LogError("[HoaCopy] Instantiate DroppedCard prefab thất bại."); return; }
                    Vector3 startPos = __instance.transform.position + new Vector3(UnityEngine.Random.Range(-0.1f, 0.1f), UnityEngine.Random.Range(0.1f, 0.3f), 0);
                    droppedCardGO.transform.position = startPos;

                    if (droppedCardGO.GetComponent<RectTransform>() is RectTransform rect) rect.localScale = Vector3.one;

                    DroppedCard droppedCard = droppedCardGO.GetComponent<DroppedCard>();
                    if (droppedCard == null) { Instance.Log.LogError("[HoaCopy] Prefab DroppedCard thiếu component DroppedCard."); UnityEngine.Object.Destroy(droppedCardGO); return; }

                    droppedCard.thePlantType = consumedType;
                    droppedCard.theSeedCost = cost;
                    droppedCard.fullCD = 0f;
                    droppedCard.CD = 0f;
                    droppedCard.isAvailable = true;
                    droppedCard.isPickUp = false;
                    droppedCard.movingWay = 0;

                    try { Lawnf.ChangeCardSprite(consumedType, droppedCardGO); }
                    catch (Exception ex) { Instance.Log.LogError($"[HoaCopy] Lỗi khi gọi Lawnf.ChangeCardSprite: {ex}"); }

                    if (droppedCardGO.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) is TMPro.TextMeshProUGUI costText) costText.text = cost.ToString();

                    if (droppedCardGO.GetComponentInChildren<Collider2D>(true) is Collider2D cardCollider) cardCollider.enabled = true;

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
                            Instance.Log.LogWarning($"[HoaCopy Patch OnMouseDown] Gọi PickUp() cho {__instance.thePlantType} trả về false.");
                        }
                    }
                }
            }
        }
        #endregion
    }
}
// --- END OF FILE Core.cs ---