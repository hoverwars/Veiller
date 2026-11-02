package com.veiller.app.data.health

import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.ActiveCaloriesBurnedRecord
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import com.veiller.app.core.model.HealthDataType
import kotlin.reflect.KClass

/**
 * Tipo de registro y permisos de lectura de Health Connect por tipo de dato.
 *
 * Solo se mapean los tipos que Veiller lee hoy. Ubicación y tiempo en pantalla no vienen
 * de Health Connect; SpO₂, presión arterial y ECG aún no se leen, así que no se piden.
 * Cada permiso que se agregue aquí debe declararse en el manifest.
 */
object HealthPermissions {

    fun recordType(type: HealthDataType): KClass<out Record>? = when (type) {
        HealthDataType.HEART_RATE -> HeartRateRecord::class
        HealthDataType.SLEEP -> SleepSessionRecord::class
        HealthDataType.STEPS -> StepsRecord::class
        HealthDataType.ACTIVE_CALORIES -> ActiveCaloriesBurnedRecord::class
        HealthDataType.LOCATION,
        HealthDataType.SCREEN_TIME,
        HealthDataType.BLOOD_PRESSURE,
        HealthDataType.SPO2,
        HealthDataType.ECG,
        -> null
    }

    fun forType(type: HealthDataType): Set<String> =
        recordType(type)?.let { setOf(HealthPermission.getReadPermission(it)) }.orEmpty()

    fun forTypes(types: Collection<HealthDataType>): Set<String> = types.flatMapTo(mutableSetOf(), ::forType)

    /** Tipos leídos por Health Connect a los que les falta algún permiso en [granted]. */
    fun missing(types: Collection<HealthDataType>, granted: Set<String>): Set<HealthDataType> =
        types.filterTo(mutableSetOf()) { type ->
            val needed = forType(type)
            needed.isNotEmpty() && !granted.containsAll(needed)
        }
}
