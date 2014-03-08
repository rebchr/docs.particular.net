﻿using NServiceBus.Config;
using NServiceBus.Unicast;
// start code ChangeThroughputV3
public class ChangeThroughput : IWantToRunWhenConfigurationIsComplete
{
    public UnicastBus Bus { get; set; }

    public void Run()
    {
        Bus.Transport.ChangeMaximumMessageThroughputPerSecond(10);
    }
}
// end code ChangeThroughputV3