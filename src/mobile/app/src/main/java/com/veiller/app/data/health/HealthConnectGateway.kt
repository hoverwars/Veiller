package com.veiller.app.data.health

import android.content.Context
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.aggregate.AggregationResult
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import java.time.Instant
import kotlin.reflect.KClass

/**
 * Envoltorio delgado de [HealthConnectClient]. No decide nada: el repositorio
 * interpreta los resultados y maneja los errores.
 */
class HealthConnectGateway(context: Context) {
    private val appContext = context.applicationContext

    // Solo se crea cuando ya se comprobó que Health Connect está disponible.
    private val client: HealthConnectClient by lazy { HealthConnectClient.getOrCreate(appContext) }

    fun sdkStatus(): Int = HealthConnectClient.getSdkStatus(appContext)

    suspend fun grantedPermissions(): Set<String> = client.permissionController.getGrantedPermissions()

    /** Los registros más recientes primero, hasta [HealthConfig.LATEST_RECORDS_PAGE_SIZE]. */
    suspend fun <T : Record> latestRecords(type: KClass<T>, start: Instant, end: Instant): List<T> =
        client.readRecords(
            ReadRecordsRequest(
                recordType = type,
                timeRangeFilter = TimeRangeFilter.between(start, end),
                ascendingOrder = false,
                pageSize = HealthConfig.LATEST_RECORDS_PAGE_SIZE,
            ),
        ).records

    suspend fun stepsTotal(start: Instant, end: Instant): AggregationResult =
        client.aggregate(
            AggregateRequest(
                metrics = setOf(StepsRecord.COUNT_TOTAL),
                timeRangeFilter = TimeRangeFilter.between(start, end),
            ),
        )
}
