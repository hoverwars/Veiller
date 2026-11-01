package com.veiller.app.data.health

import java.time.Duration

/** Parámetros de lectura de Health Connect. Ajustar aquí, no en el código que los usa. */
object HealthConfig {
    /** Hasta dónde se busca la frecuencia cardiaca más reciente. */
    val HEART_RATE_LOOKBACK: Duration = Duration.ofDays(3)

    /** Hasta dónde se busca la última sesión de sueño. */
    val SLEEP_LOOKBACK: Duration = Duration.ofDays(14)

    /** Registros que se piden por lectura; basta con los más recientes. */
    const val LATEST_RECORDS_PAGE_SIZE = 20

    /**
     * Antigüedad a partir de la cual la FC se considera desactualizada. Solo la FC
     * decide "Sin datos recientes del reloj": es continua, y un adulto mayor quieto no
     * genera pasos. Provisional: Mi Fitness escribe cuando el reloj sincroniza, sin
     * intervalo garantizado; revisar con la latencia medida en el dispositivo.
     */
    val HEART_RATE_STALE_AFTER: Duration = Duration.ofHours(2)

    /** Una sesión de sueño que terminó hace más que esto no es la de la última noche. */
    val SLEEP_STALE_AFTER: Duration = Duration.ofHours(36)

    /** Proveedor de Health Connect en Android 13 o menos (app de Play Store). */
    const val PROVIDER_PACKAGE = "com.google.android.apps.healthdata"
    const val PLAY_STORE_PACKAGE = "com.android.vending"

    /** Enlace de Play Store que instala o actualiza Health Connect y vuelve al onboarding. */
    const val PROVIDER_STORE_URI =
        "market://details?id=$PROVIDER_PACKAGE&url=healthconnect%3A%2F%2Fonboarding"
}
