﻿namespace Core6.Recoverability.Immediate
{
    using NServiceBus;

    public class Usage
    {
        void ConfigureFlr(EndpointConfiguration endpointConfiguration)
        {
            #region FlrCodeFirstConfiguration

            var recoverability = endpointConfiguration.Recoverability();
            recoverability.Immediate(
                immediate =>
                {
                    immediate.NumberOfRetries(3);
                });

            #endregion
        }
    }
}