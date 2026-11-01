package com.veiller.app.data.health

import android.os.RemoteException
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import com.veiller.app.core.model.HealthDataType
import com.veiller.app.data.repository.HealthRepository
import java.io.IOException
import java.time.Clock
import java.time.LocalDate
import kotlin.coroutines.cancellation.CancellationException

/** Lee de Health Connect en el teléfono del adulto mayor. No guarda nada localmente. */
class HealthConnectRepository(
    private val gateway: HealthConnectGateway,
    private val clock: Clock = Clock.systemDefaultZone(),
) : HealthRepository {

    override fun availability(): HealthAvailability = HealthRecordMappers.availability(gateway.sdkStatus())

    override fun permissionsFor(types: Collection<HealthDataType>): Set<String> = HealthPermissions.forTypes(types)

    override suspend fun missingPermissions(types: Collection<HealthDataType>): Set<HealthDataType> {
        val granted = if (availability() == HealthAvailability.AVAILABLE) grantedOrNull() else null
        return HealthPermissions.missing(types, granted.orEmpty())
    }

    override suspend fun readToday(): HealthSnapshot {
        val now = clock.instant()
        val granted = if (availability() == HealthAvailability.AVAILABLE) grantedOrNull() else null
        if (granted == null) {
            return HealthSnapshot(HealthMetric.ReadFailed, HealthMetric.ReadFailed, HealthMetric.ReadFailed, now)
        }
        fun permitted(type: HealthDataType) = granted.containsAll(HealthPermissions.forType(type))

        val startOfDay = LocalDate.now(clock).atStartOfDay(clock.zone).toInstant()
        return HealthSnapshot(
            heartRate = readMetric(permitted(HealthDataType.HEART_RATE)) {
                val records = gateway.latestRecords(HeartRateRecord::class, now.minus(HealthConfig.HEART_RATE_LOOKBACK), now)
                HealthRecordMappers.latestHeartRate(records)
            },
            steps = readMetric(permitted(HealthDataType.STEPS)) {
                val total = gateway.stepsTotal(startOfDay, now)
                val records = gateway.latestRecords(StepsRecord::class, startOfDay, now)
                HealthRecordMappers.todaySteps(total[StepsRecord.COUNT_TOTAL], total.dataOrigins, records)
            },
            sleep = readMetric(permitted(HealthDataType.SLEEP)) {
                val records = gateway.latestRecords(SleepSessionRecord::class, now.minus(HealthConfig.SLEEP_LOOKBACK), now)
                HealthRecordMappers.latestSleep(records)
            },
            readAt = now,
        )
    }

    private suspend fun grantedOrNull(): Set<String>? = try {
        gateway.grantedPermissions()
    } catch (e: CancellationException) {
        throw e
    } catch (_: RemoteException) {
        null
    } catch (_: IOException) {
        null
    } catch (_: IllegalStateException) {
        null
    }

    /**
     * Cada tipo se lee por separado: un permiso revocado ([SecurityException]) o un
     * fallo de Health Connect afectan solo a esa métrica, sin tumbar la pantalla.
     */
    private suspend fun <T> readMetric(permitted: Boolean, read: suspend () -> T?): HealthMetric<T> {
        if (!permitted) return HealthMetric.NotPermitted
        return try {
            read()?.let { HealthMetric.Value(it) } ?: HealthMetric.NoData
        } catch (e: CancellationException) {
            // CancellationException hereda de IllegalStateException: hay que dejarla pasar.
            throw e
        } catch (_: SecurityException) {
            HealthMetric.NotPermitted
        } catch (_: RemoteException) {
            HealthMetric.ReadFailed
        } catch (_: IOException) {
            HealthMetric.ReadFailed
        } catch (_: IllegalStateException) {
            // Así llega el límite de cuota de Health Connect.
            HealthMetric.ReadFailed
        }
    }
}
