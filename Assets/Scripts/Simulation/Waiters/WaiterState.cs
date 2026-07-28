public enum WaiterState
{
    Idle,
    WalkingToTable,
    TakingOrder,
    WalkingToKitchen,
    WaitingForDish,
    WalkingToServeTable,
    ServingFood,
    WalkingToBill,
    DeliveringBill,
    WalkingToCleanTable,
    CleaningTable,

    // 367H: atención independiente de barra.
    WalkingToBar,
    TakingBarOrder,
    WalkingToBarBill,
    DeliveringBarBill
}
