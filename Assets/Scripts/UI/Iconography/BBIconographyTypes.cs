using System;
using UnityEngine;

namespace BistroBuilder.UI.Iconography
{
    public enum BBIconState
    {
        Normal = 0,
        Hover = 1,
        Selected = 2,
        Disabled = 3
    }

    public enum BBIconSemanticRole
    {
        Neutral = 0,
        Positive = 1,
        Warning = 2,
        Critical = 3,
        Information = 4,
        Accent = 5
    }

    /// <summary>
    /// Identificadores canónicos. La UI solicita conceptos, nunca rutas de assets.
    /// </summary>
    public enum BBIconId
    {
        // 01. Navegación principal
        NavActivity,
        NavStaff,
        NavMenu,
        NavInventory,
        NavSuppliers,
        NavReservations,
        NavEconomy,
        NavMarketing,
        NavReputation,
        NavEditMode,
        NavOptions,

        // 03. Áreas y objetos
        AreaDining,
        AreaTerrace,
        AreaBar,
        AreaKitchen,
        AreaBathrooms,
        AreaStorage,
        AreaEntrance,
        AreaCashRegister,
        AreaWaste,
        AreaCleaning,
        ObjectTable,
        ObjectChair,
        ObjectWaiter,
        ObjectCook,
        ObjectDish,
        ObjectIngredient,
        ObjectDrink,
        ObjectEquipment,
        ObjectDecoration,
        ObjectLighting,

        // 04. Acciones
        ActionAdd,
        ActionEdit,
        ActionDuplicate,
        ActionDelete,
        ActionSave,
        ActionCancel,
        ActionMove,
        ActionRotate,
        ActionConfirm,
        ActionPrioritize,
        ActionPause,
        ActionResume,

        // 05. Estados
        StatusCorrect,
        StatusAttention,
        StatusCritical,
        StatusInformation,
        StatusWaiting,
        StatusProcessing,
        StatusPaused,
        StatusBlocked,
        StatusReserved,
        StatusGroup,

        // 06. Clientes y reservas
        CustomerClient,
        CustomerGroup,
        CustomerChild,
        CustomerHighChair,
        CustomerPet,
        CustomerReservation,
        CustomerVip,

        // 07. Comida y bebida
        FoodStarter,
        FoodMain,
        FoodDessert,
        FoodDrink,
        FoodCoffee,
        FoodWine,
        FoodBeer,
        FoodVegetarian,
        FoodVegan,
        FoodGlutenFree,

        // 08. Economía
        EconomyIncome,
        EconomyExpenses,
        EconomyProfit,
        EconomyReport,
        EconomyInvoice,

        // 09. Direccionales y generales
        GeneralBack,
        GeneralNext,
        GeneralUp,
        GeneralDown,
        GeneralHelp,
        GeneralMore,

        // 10. Indicadores en escena
        SceneTableServed,
        SceneWaiting,
        SceneProblem,
        SceneGroup,
        SceneReservation,
        SceneCleaning
    }

    public static class BBIconDesignTokens
    {
        public const float MinSize = 16f;
        public const float SmallSize = 24f;
        public const float StandardSize = 32f;
        public const float LargeSize = 48f;

        public const float HoverScale = 1.055f;
        public const float HoverLift = 2f;
        public const float TransitionSpeed = 14f;

        public static readonly Color Neutral = FromHex("#DCE3E5");
        public static readonly Color Text = FromHex("#EDF1F1");
        public static readonly Color Muted = FromHex("#98A7AC");
        public static readonly Color Border = FromHex("#29434A");
        public static readonly Color HoverBorder = FromHex("#72878E");
        public static readonly Color Selected = FromHex("#E2B960");
        public static readonly Color SelectedSoft = FromHex("#F1D38B");
        public static readonly Color Disabled = FromHex("#53656B");
        public static readonly Color Positive = FromHex("#64D584");
        public static readonly Color Warning = FromHex("#EFC45D");
        public static readonly Color Critical = FromHex("#EF6659");
        public static readonly Color Information = FromHex("#70B9E8");

        public static Color ForRole(BBIconSemanticRole role)
        {
            switch (role)
            {
                case BBIconSemanticRole.Positive: return Positive;
                case BBIconSemanticRole.Warning: return Warning;
                case BBIconSemanticRole.Critical: return Critical;
                case BBIconSemanticRole.Information: return Information;
                case BBIconSemanticRole.Accent: return Selected;
                default: return Neutral;
            }
        }

        private static Color FromHex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        }
    }

    [Serializable]
    public struct BBIconDefinition
    {
        public BBIconId id;
        public Sprite sprite;
        public BBIconSemanticRole semanticRole;
        public string sourceName;

        public BBIconDefinition(BBIconId id, Sprite sprite, BBIconSemanticRole semanticRole, string sourceName)
        {
            this.id = id;
            this.sprite = sprite;
            this.semanticRole = semanticRole;
            this.sourceName = sourceName;
        }
    }
}
