package com.veiller.app.data.health

import com.veiller.app.core.model.HealthDataType
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
     * Una FC más vieja que esto se muestra como "Última medición registrada". No implica
     * que el reloj esté apagado: Mi Fitness solo escribe en Health Connect la FC de los
     * entrenamientos.
     */
    val HEART_RATE_STALE_AFTER: Duration = Duration.ofHours(2)

    /** Una sesión de sueño que terminó hace más que esto no es la de la última noche. */
    val SLEEP_STALE_AFTER: Duration = Duration.ofHours(36)

    /** Paquete de Mi Fitness, la app del Redmi Watch 5 Lite (verificado con Mi Fitness 3.59.2i). */
    const val MI_FITNESS_PACKAGE = "com.xiaomi.wearable"

    /** Apps de reloj cuyos registros cuentan como señal de vida (dataOrigin.packageName). */
    val WATCH_APP_PACKAGES: Set<String> = setOf(MI_FITNESS_PACKAGE)

    /**
     * Señal de vida del reloj: el fin efectivo más reciente de los registros de estos tipos
     * escritos por una app de [WATCH_APP_PACKAGES], es decir min(fin, última modificación,
     * ahora). Decide "Sin datos recientes del reloj".
     */
    val WATCH_LIVENESS_TYPES: Set<HealthDataType> =
        setOf(HealthDataType.HEART_RATE, HealthDataType.STEPS, HealthDataType.ACTIVE_CALORIES)

    /** Hasta dónde se busca la señal de vida. */
    val WATCH_LIVENESS_LOOKBACK: Duration = Duration.ofDays(3)

    /** Sin señal de vida durante este tiempo, el reloj se considera sin datos recientes. Provisional. */
    val WATCH_LIVENESS_STALE_AFTER: Duration = Duration.ofHours(2)

    /** Proveedor de Health Connect en Android 13 o menos (app de Play Store). */
    const val PROVIDER_PACKAGE = "com.google.android.apps.healthdata"
    const val PLAY_STORE_PACKAGE = "com.android.vending"

    /** Enlace de Play Store que instala o actualiza Health Connect y vuelve al onboarding. */
    const val PROVIDER_STORE_URI =
        "market://details?id=$PROVIDER_PACKAGE&url=healthconnect%3A%2F%2Fonboarding"
}
