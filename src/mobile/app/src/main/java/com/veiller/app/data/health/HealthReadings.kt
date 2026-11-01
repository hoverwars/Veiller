package com.veiller.app.data.health

import java.time.Duration
import java.time.Instant

/*
 * Lecturas de salud del adulto mayor, sin tipos de Health Connect.
 * Cada lectura conserva el paquete que la escribió (p. ej. Mi Fitness): hoy no se
 * filtra por origen, pero sirve para detectar duplicados cuando haya más de una app.
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
    fun isStale(now: Instant): Boolean = isOlderThan(measuredAt, now, HealthConfig.HEART_RATE_STALE_AFTER)
}

/**
 * Pasos de hoy. [count] sale del agregado (deduplica entre orígenes); [lastRecordAt]
 * es el fin del registro más reciente, o null si Health Connect no lo devolvió.
 * Nunca se marcan como desactualizados: sin movimiento no hay registros.
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

    fun isStale(now: Instant): Boolean = isOlderThan(end, now, HealthConfig.SLEEP_STALE_AFTER)
}

data class HealthSnapshot(
    val heartRate: HealthMetric<HeartRateReading>,
    val steps: HealthMetric<StepsReading>,
    val sleep: HealthMetric<SleepReading>,
    /** Momento de la lectura: referencia para calcular "hace X". */
    val readAt: Instant,
) {
    /**
     * Último dato del reloj. Solo cuenta la FC, que es continua: los pasos dependen de
     * que la persona se mueva y el sueño se escribe una vez al día.
     */
    val lastWatchDataAt: Instant?
        get() = (heartRate as? HealthMetric.Value)?.reading?.measuredAt

    val isWatchDataStale: Boolean
        get() = lastWatchDataAt?.let { isOlderThan(it, readAt, HealthConfig.HEART_RATE_STALE_AFTER) } ?: true
}

fun isOlderThan(instant: Instant, now: Instant, threshold: Duration): Boolean =
    Duration.between(instant, now) > threshold
