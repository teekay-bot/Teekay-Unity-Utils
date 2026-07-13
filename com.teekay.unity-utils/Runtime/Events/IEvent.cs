namespace TeekayUtils.Events
{
    /// <summary>
    /// Marker for a message that can travel through the <see cref="EventBus"/>. Implement it on a
    /// <b>struct</b> — the bus constrains its generic methods to <c>where T : struct, IEvent</c>, which
    /// (a) stops arbitrary types (int, Vector3…) being published by accident, and (b) keeps publishing
    /// allocation-free. Events should be small, immutable value types carrying just the intent's data.
    /// </summary>
    public interface IEvent { }
}
