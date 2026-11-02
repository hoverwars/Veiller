package com.veiller.app.data.health

import com.veiller.app.core.model.HealthDataType
import java.time.Duration
import java.time.Instant

/*
 * Lecturas de salud del adulto mayor, sin tipos de Health Connect.
 * Cada lectura conserva el paquete que la escribió (p. ej. Mi Fitness): las métricas no
 * se filtran por origen, pero la señal de vida del reloj sí.
 */

enum class HealthAvailability {
    AVAILABLE,

    /** Android 13 o menos: falta instalar o actualizar Health Connect desde Play Store. */
    UPDATE_REQUIRED,
    UNAVAILABLE,
}

/** Estado de una métrica tras intentar leerla. */
sealed interface HealthMetric<out T> {
    /** El usuario no concedió el permiso de Health Connect para este tipo. */
    data object NotPermitted : HealthMetric<Nothing>

    /** Hay permiso, pero Health Connect no tiene registros en la ventana de lectura. */
    data object NoData : HealthMetric<Nothing>

    /** Health Connect falló (cuota, servicio caído...). Se puede reintentar. */
    data object ReadFailed : HealthMetric<Nothing>

    data class Value<T>(val reading: T) : HealthMetric<T>
}

data class HeartRateReading(
    val beatsPerMinute: Long,
    val measuredAt: Instant,
    val sourcePackage: String,
) {
    /** Vieja no es lo mismo que reloj apagado: solo cambia cómo se rotula en la tarjeta. */
    fun isOld(now: Instant): Boolean = isOlderThan(measuredAt, now, HealthConfig.HEART_RATE_STALE_AFTER)
}

/**
 * Pasos de hoy. [count] sale del agregado (deduplica entre orígenes); [lastRecordAt]
 * es el fin del registro más reciente, o null si Health Connect no lo devolvió.
 */
data class StepsReading(
    val count: Long,
    val lastRecordAt: Instant?,
    val sourcePackages: Set<String>,
)

data class SleepReading(
    val start: Instant,
    val end: Instant,
    val sourcePackage: String,
) {
    val duration: Duration get() = Duration.between(start, end)

    fun isOld(now: Instant): Boolean = isOlderThan(end, now, HealthConfig.SLEEP_STALE_AFTER)
}

data class HealthSnapshot(
    val heartRate: HealthMetric<HeartRateReading>,
    val steps: HealthMetric<StepsReading>,
    val sleep: HealthMetric<SleepReading>,
    /**
     * Señal de vida del reloj: fin del registro más reciente de [HealthConfig.WATCH_LIVENESS_TYPES]
     * escrito por una app de [HealthConfig.WATCH_APP_PACKAGES]; null si no hay ninguno.
     */
    val lastWatchDataAt: Instant?,
    /** Tipos leídos por Health Connect sin permiso concedido. */
    val missing: Set<HealthDataType>,
    /** Momento de la lectura: referencia para calcular "hace X". */
    val readAt: Instant,
) {
    /** Única regla de "Sin datos recientes del reloj", para el encabezado y las tarjetas. */
    val isWatchDataStale: Boolean
        get() = lastWatchDataAt?.let { isOlderThan(it, readAt, HealthConfig.WATCH_LIVENESS_STALE_AFTER) } ?: true
}

fun isOlderThan(instant: Instant, now: Instant, threshold: Duration): Boolean =
    Duration.between(instant, now) > threshold
