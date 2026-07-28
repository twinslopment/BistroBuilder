public enum CustomerGroupState
{
    Entering,
    WaitingForTable,
    WalkingToTable,
    Seated,
    WaitingForWaiter,
    Ordering,
    WaitingForFood,
    Eating,
    WaitingForBill,
    Paying,
    Leaving,
    Finished,

    // Estados exclusivos del servicio completo en barra. WaitingAtBar no usa
    // un estado principal propio: conserva WaitingForTable para mantener su
    // posición en la cola y se distingue mediante CurrentServiceMode.
    WalkingToBar,
    WaitingForBarOrder,
    OrderingAtBar,
    WaitingForBarItems,
    ConsumingAtBar,
    PayingAtBar
}
