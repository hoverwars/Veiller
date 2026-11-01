package com.veiller.app.data.health

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class HealthReadingsTest {
    private val now = Instant.parse("2026-11-01T22:30:00Z")
    private val source = "com.example.reloj"

    @Test fun `FC vieja queda desactualizada`() {
        assertFalse(HeartRateReading(70, now.minus(HealthConfig.HEART_RATE_STALE_AFTER), source).isStale(now))
        assertTrue(HeartRateReading(70, now.minus(HealthConfig.HEART_RATE_STALE_AFTER).minusSeconds(1), source).isStale(now))
    }

    @Test fun `sueño usa su propio umbral`() {
        val end = now.minus(HealthConfig.HEART_RATE_STALE_AFTER).minusSeconds(1)
        assertFalse(SleepReading(end.minusSeconds(3600), end, source).isStale(now))
        val old = now.minus(HealthConfig.SLEEP_STALE_AFTER).minusSeconds(1)
        assertTrue(SleepReading(old.minusSeconds(3600), old, source).isStale(now))
    }

    @Test fun `solo la FC decide si los datos del reloj están desactualizados`() {
        val heart = now.minusSeconds(3 * 3600)
        val steps = now.minusSeconds(60)
        val snapshot = HealthSnapshot(
            heartRate = HealthMetric.Value(HeartRateReading(70, heart, source)),
            steps = HealthMetric.Value(StepsReading(100, steps, setOf(source))),
            sleep = HealthMetric.NoData,
            readAt = now,
        )
        assertEquals(heart, snapshot.lastWatchDataAt)
        assertTrue(snapshot.isWatchDataStale)
    }

    @Test fun `pasos viejos no generan alarma si la FC está al día`() {
        val snapshot = HealthSnapshot(
            heartRate = HealthMetric.Value(HeartRateReading(70, now.minusSeconds(60), source)),
            steps = HealthMetric.Value(StepsReading(100, now.minusSeconds(8 * 3600), setOf(source))),
            sleep = HealthMetric.NoData,
            readAt = now,
        )
        assertFalse(snapshot.isWatchDataStale)
    }

    @Test fun `sin FC se considera desactualizado aunque haya pasos`() {
        val snapshot = HealthSnapshot(
            heartRate = HealthMetric.NoData,
            steps = HealthMetric.Value(StepsReading(100, now.minusSeconds(60), setOf(source))),
            sleep = HealthMetric.NoData,
            readAt = now,
        )
        assertNull(snapshot.lastWatchDataAt)
        assertTrue(snapshot.isWatchDataStale)
    }

    @Test fun `sin datos del reloj se considera desactualizado`() {
        val snapshot = HealthSnapshot(HealthMetric.NotPermitted, HealthMetric.NoData, HealthMetric.ReadFailed, now)
        assertNull(snapshot.lastWatchDataAt)
        assertTrue(snapshot.isWatchDataStale)
    }
}
