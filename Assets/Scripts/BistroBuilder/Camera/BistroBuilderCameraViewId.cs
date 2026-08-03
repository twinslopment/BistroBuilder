namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Identidad estable de las vistas canónicas de cámara 369B.
    /// None representa navegación libre y no es un preset seleccionable.
    /// </summary>
    public enum BistroBuilderCameraViewId
    {
        None = 0,
        General = 1,
        Isometric = 2,
        TopDown = 3,
        Close = 4
    }

    public enum BistroBuilderCameraViewFocusMode
    {
        BoundsCenter = 0,
        CurrentFocus = 1
    }

    public enum BistroBuilderCameraViewYawMode
    {
        Fixed = 0,
        PreserveCurrent = 1
    }

    public enum BistroBuilderCameraViewFramingMode
    {
        FitRestaurantBounds = 0,
        FixedDistance = 1
    }
}
