package com.veiller.app.data.health

import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.records.metadata.DataOrigin
import androidx.health.connect.client.records.metadata.Metadata
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.time.Duration
import java.time.Instant
import java.time.ZoneOffset

/*
 * El origen (dataOrigin) lo asigna Health Connect al guardar y su constructor es interno,
 * así que en estos registros llega vacío: se comprueba que el mapper lo copia del registro.
 */
class HealthRecordMappersTest {
    private val offset = ZoneOffset.ofHours(-5)
    private val t0 = Instant.parse("2026-11-01T12:50:00Z")

    private fun heartRate(start: Instant, vararg samples: Pair<Long, Long>) = HeartRateRecord(
        startTime = start,
        startZoneOffset = offset,
        endTime = start.plus(Duration.ofMinutes(10)),
        endZoneOffset = offset,
        samples = samples.map { (minute, bpm) -> HeartRateRecord.Sample(start.plus(Duration.ofMinutes(minute)), bpm) },
        metadata = Metadata.manualEntry(),
    )

    private fun steps(start: Instant, end: Instant) =
        StepsRecord(start, offset, end, offset, count = 100, metadata = Metadata.manualEntry())

    private fun sleep(start: Instant, end: Instant) =
        SleepSessionRecord(start, offset, end, offset, metadata = Metadata.manualEntry())

    @Test fun `estado del SDK`() {
        assertEquals(HealthAvailability.AVAILABLE, HealthRecordMappers.availability(HealthConnectClient.SDK_AVAILABLE))
        assertEquals(
            HealthAvailability.UPDATE_REQUIRED,
            HealthRecordMappers.availability(HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED),
        )
        assertEquals(HealthAvailability.UNAVAILABLE, HealthRecordMappers.availability(HealthConnectClient.SDK_UNAVAILABLE))
    }

    @Test fun `FC toma la muestra más reciente entre registros y conserva su origen`() {
        val latest = heartRate(t0.plus(Duration.ofMinutes(20)), 2L to 145L)
        val records = listOf(heartRate(t0, 1L to 80L, 5L to 90L), latest)
        val reading = HealthRecordMappers.latestHeartRate(records)
        assertEquals(
            HeartRateReading(145, t0.plus(Duration.ofMinutes(22)), latest.metadata.dataOrigin.packageName),
            reading,
        )
    }

    @Test fun `FC sin registros es null`() {
        assertNull(HealthRecordMappers.latestHeartRate(emptyList()))
    }

    @Test fun `pasos usan el agregado y la hora del último registro, no la de la consulta`() {
        val records = listOf(
            steps(t0, t0.plus(Duration.ofMinutes(15))),
            steps(t0.plus(Duration.ofMinutes(30)), t0.plus(Duration.ofMinutes(46))),
        )
        val origins = setOf(DataOrigin("com.example.reloj"), DataOrigin("com.example.otra"))
        val reading = HealthRecordMappers.todaySteps(8894, origins, records)
        assertEquals(
            StepsReading(8894, t0.plus(Duration.ofMinutes(46)), setOf("com.example.reloj", "com.example.otra")),
            reading,
        )
    }

    @Test fun `pasos sin agregado es null aunque haya registros`() {
        assertNull(HealthRecordMappers.todaySteps(null, emptySet(), listOf(steps(t0, t0.plusSeconds(60)))))
    }

    @Test fun `pasos sin registros quedan sin hora`() {
        assertNull(HealthRecordMappers.todaySteps(10, emptySet(), emptyList())?.lastRecordAt)
    }

    @Test fun `sueño toma la sesión que terminó más tarde`() {
        val night = t0.minus(Duration.ofHours(14))
        val previous = night.minus(Duration.ofDays(1))
        val last = sleep(night, night.plus(Duration.ofHours(6)))
        val reading = HealthRecordMappers.latestSleep(listOf(sleep(previous, previous.plus(Duration.ofHours(7))), last))
        assertEquals(SleepReading(night, night.plus(Duration.ofHours(6)), last.metadata.dataOrigin.packageName), reading)
        assertEquals(Duration.ofHours(6), reading?.duration)
    }

    @Test fun `sueño sin sesiones es null`() {
        assertNull(HealthRecordMappers.latestSleep(emptyList()))
    }
}
