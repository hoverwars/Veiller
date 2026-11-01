package com.veiller.app.data.health

import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.records.metadata.DataOrigin
import java.time.Instant

/** Registros de Health Connect -> lecturas de dominio. Funciones puras. */
object HealthRecordMappers {

    fun availability(sdkStatus: Int): HealthAvailability = when (sdkStatus) {
        HealthConnectClient.SDK_AVAILABLE -> HealthAvailability.AVAILABLE
        HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED -> HealthAvailability.UPDATE_REQUIRED
        else -> HealthAvailability.UNAVAILABLE
    }

    /** La muestra más reciente entre todos los registros, con el origen de su registro. */
    fun latestHeartRate(records: List<HeartRateRecord>): HeartRateReading? =
        records
            .flatMap { record -> record.samples.map { sample -> sample to record.metadata.dataOrigin } }
            .maxByOrNull { (sample, _) -> sample.time }
            ?.let { (sample, origin) -> HeartRateReading(sample.beatsPerMinute, sample.time, origin.packageName) }

    /**
     * [total] es el agregado del día; los [records] solo dan la hora del último registro,
     * para no mostrar la hora de la consulta como si fuera la de la medición.
     */
    fun todaySteps(total: Long?, origins: Set<DataOrigin>, records: List<StepsRecord>): StepsReading? {
        if (total == null) return null
        return StepsReading(
            count = total,
            lastRecordAt = records.maxOfOrNull { it.endTime },
            sourcePackages = origins.mapTo(mutableSetOf()) { it.packageName },
        )
    }

    /**
     * Fin del registro más reciente escrito por alguna de las apps de [watchPackages].
     * [endOf] da el fin de cada tipo (IntervalRecord es interno en la librería).
     */
    fun <T : Record> latestEndFrom(records: List<T>, watchPackages: Set<String>, endOf: (T) -> Instant): Instant? =
        records.filter { it.metadata.dataOrigin.packageName in watchPackages }.maxOfOrNull(endOf)

    fun latestSleep(records: List<SleepSessionRecord>): SleepReading? =
        records.maxByOrNull { it.endTime }?.let {
            SleepReading(start = it.startTime, end = it.endTime, sourcePackage = it.metadata.dataOrigin.packageName)
        }
}
