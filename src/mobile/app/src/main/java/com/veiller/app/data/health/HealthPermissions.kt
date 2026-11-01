package com.veiller.app.data.health

import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import com.veiller.app.core.model.HealthDataType

/**
 * Permisos de lectura de Health Connect por tipo de dato.
 *
 * Solo se mapean los tipos que Veiller lee hoy (F1). Ubicación y tiempo en pantalla
 * no vienen de Health Connect; SpO₂, presión arterial y ECG aún no se leen, así que
 * no se piden. Cada permiso que se agregue aquí debe declararse en el manifest.
 */
object HealthPermissions {

    fun forType(type: HealthDataType): Set<String> = when (type) {
        HealthDataType.HEART_RATE -> setOf(HealthPermission.getReadPermission(HeartRateRecord::class))
        HealthDataType.SLEEP -> setOf(HealthPermission.getReadPermission(SleepSessionRecord::class))
        HealthDataType.STEPS -> setOf(HealthPermission.getReadPermission(StepsRecord::class))
        HealthDataType.LOCATION,
        HealthDataType.SCREEN_TIME,
        HealthDataType.BLOOD_PRESSURE,
        HealthDataType.SPO2,
        HealthDataType.ECG,
        -> emptySet()
    }

    fun forTypes(types: Collection<HealthDataType>): Set<String> = types.flatMapTo(mutableSetOf(), ::forType)

    /** Tipos leídos por Health Connect a los que les falta algún permiso en [granted]. */
    fun missing(types: Collection<HealthDataType>, granted: Set<String>): Set<HealthDataType> =
        types.filterTo(mutableSetOf()) { type ->
            val needed = forType(type)
            needed.isNotEmpty() && !granted.containsAll(needed)
        }
}
