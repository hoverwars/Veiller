package com.veiller.app.data.health

import com.veiller.app.core.model.HealthDataType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class HealthReadingsTest {
    private val now = Instant.parse("2026-11-01T22:30:00Z")
    private val source = "com.example.reloj"

    private fun snapshot(lastWatchDataAt: Instant?, heartRateAt: Instant? = null) = HealthSnapshot(
        heartRate = heartRateAt?.let { HealthMetric.Value(HeartRateReading(70, it, source)) } ?: HealthMetric.NoData,
        steps = HealthMetric.NoData,
        sleep = HealthMetric.NoData,
        lastWatchDataAt = lastWatchDataAt,
        missing = emptySet(),
        readAt = now,
    )

    @Test fun `FC vieja se rotula como última medición`() {
        assertFalse(HeartRateReading(70, now.minus(HealthConfig.HEART_RATE_STALE_AFTER), source).isOld(now))
        assertTrue(HeartRateReading(70, now.minus(HealthConfig.HEART_RATE_STALE_AFTER).minusSeconds(1), source).isOld(now))
    }

    @Test fun `sueño usa su propio umbral`() {
        val end = now.minus(HealthConfig.HEART_RATE_STALE_AFTER).minusSeconds(1)
        assertFalse(SleepReading(end.minusSeconds(3600), end, source).isOld(now))
        val old = now.minus(HealthConfig.SLEEP_STALE_AFTER).minusSeconds(1)
        assertTrue(SleepReading(old.minusSeconds(3600), old, source).isOld(now))
    }

    @Test fun `la señal de vida cuenta FC, pasos y calorías activas`() {
        assertEquals(
            setOf(HealthDataType.HEART_RATE, HealthDataType.STEPS, HealthDataType.ACTIVE_CALORIES),
            HealthConfig.WATCH_LIVENESS_TYPES,
        )
    }

    @Test fun `señal de vida dentro del umbral está al día`() {
        assertFalse(snapshot(now.minus(HealthConfig.WATCH_LIVENESS_STALE_AFTER)).isWatchDataStale)
        assertTrue(snapshot(now.minus(HealthConfig.WATCH_LIVENESS_STALE_AFTER).minusSeconds(1)).isWatchDataStale)
    }

    @Test fun `FC vieja no marca el reloj como sin datos si la señal de vida es reciente`() {
        // Caso del S25: FC de un entrenamiento hace 9 h, calorías activas del reloj hace 20 min.
        val s = snapshot(lastWatchDataAt = now.minusSeconds(20 * 60), heartRateAt = now.minusSeconds(9 * 3600))
        assertFalse(s.isWatchDataStale)
        assertTrue((s.heartRate as HealthMetric.Value).reading.isOld(now))
    }

    @Test fun `sin señal de vida se considera sin datos recientes`() {
        assertTrue(snapshot(lastWatchDataAt = null, heartRateAt = now).isWatchDataStale)
    }
}
