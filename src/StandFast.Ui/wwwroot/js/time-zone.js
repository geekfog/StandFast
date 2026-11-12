// Reports the browser's IANA time zone, and short names such as CST or CDT for any zone, or null when the browser does not have one.
window.standFastTimeZone = (() => {
    // Locales tried in turn for a short name. Each knows lettered names for its own region and writes an offset such as GMT-6 elsewhere.
    const fallbackLocales = ['en-US', 'en-GB'];
    const offsetName = /^(GMT|UTC)([+\-−]|$)/;

    const shortName = (zoneId, locale, instant) => {
        try {
            return new Intl.DateTimeFormat(locale, { timeZone: zoneId, timeZoneName: 'short' })
                .formatToParts(instant)
                .find(part => part.type === 'timeZoneName')?.value ?? null;
        } catch {
            return null;
        }
    };

    return {
        browserId: () => {
            try {
                return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
            } catch {
                return null;
            }
        },

        // The lettered short name of the zone at the given Unix time in milliseconds, or null when no locale has one.
        abbreviation: (zoneId, epochMilliseconds) => {
            const instant = new Date(epochMilliseconds);
            for (const locale of [...navigator.languages, ...fallbackLocales]) {
                const name = shortName(zoneId, locale, instant);
                if (name && !offsetName.test(name)) {
                    return name;
                }
            }
            return null;
        },
    };
})();
