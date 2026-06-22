using Microsoft.Extensions.Logging;

namespace Wiretap.Util;

public static class DiagnosticWarnings
{
    public static void WarnAboutCustomStatusName
    (
        this DiagnosticLogger logger,
        string statusName,
        string canonicalName
    )
    {
        if (statusName == canonicalName)
        {
            return;
        }

        logger.WarnOnce(nameof(WarnAboutCustomStatusName), statusName, log => log.LogWarning(
            "{StatusName} will be logged as {CanonicalName} because only canonical status names are allowed. Rename {StatusNameToRename} to {CanonicalNameToUse} to get rid of this warning.",
            statusName,
            canonicalName,
            statusName,
            canonicalName
        ));
    }

    public static void WarnAboutMissingConfigurationVariant
    (
        this DiagnosticLogger logger,
        string variant,
        string activityType
    )
    {
        logger.WarnOnce(nameof(WarnAboutMissingConfigurationVariant), (variant, activityType), log => log.LogWarning(
            "Configuration variant {Variant} requested by {ActivityType} was not found; using the default variant.",
            variant,
            activityType
        ));
    }

    public static void WarnAboutLastStatusOverwrite
    (
        this DiagnosticLogger logger,
        string activityName,
        string currentStatus,
        string ignoredStatus
    )
    {
        logger.WarnOnce(
            nameof(WarnAboutLastStatusOverwrite),
            (activityName, currentStatus, ignoredStatus),
            log => log.LogWarning(
                "{ActivityName} status was already set to [{CurrentStatus}]; ignored later status [{IgnoredStatus}].",
                activityName,
                currentStatus,
                ignoredStatus
            )
        );
    }
}
