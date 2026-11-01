package com.veiller.app.data.health

import com.veiller.app.core.model.HealthDataType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class HealthPermissionsTest {
    private val heartRate = "android.permission.health.READ_HEART_RATE"
    private val steps = "android.permission.health.READ_STEPS"
    private val sleep = "android.permission.health.READ_SLEEP"
    private val activeCalories = "android.permission.health.READ_ACTIVE_CALORIES_BURNED"
    private val all = setOf(heartRate, steps, sleep, activeCalories)

    @Test fun `cada tipo leído pide su permiso de lectura`() {
        assertEquals(setOf(heartRate), HealthPermissions.forType(HealthDataType.HEART_RATE))
        assertEquals(setOf(steps), HealthPermissions.forType(HealthDataType.STEPS))
        assertEquals(setOf(sleep), HealthPermissions.forType(HealthDataType.SLEEP))
        assertEquals(setOf(activeCalories), HealthPermissions.forType(HealthDataType.ACTIVE_CALORIES))
    }

    @Test fun `calorías activas es obligatorio`() {
        assertTrue(HealthDataType.ACTIVE_CALORIES.required)
    }

    @Test fun `los tipos que no vienen de Health Connect no piden nada`() {
        listOf(HealthDataType.LOCATION, HealthDataType.SCREEN_TIME, HealthDataType.ECG).forEach {
            assertTrue(HealthPermissions.forType(it).isEmpty())
        }
    }

    @Test fun `todos los obligatorios de Health Connect quedan cubiertos`() {
        assertEquals(all, HealthPermissions.forTypes(HealthDataType.entries))
    }

    @Test fun `los tipos de la señal de vida se pueden leer por Health Connect`() {
        HealthConfig.WATCH_LIVENESS_TYPES.forEach { assertTrue(HealthPermissions.recordType(it) != null) }
    }

    @Test fun `detecta los permisos que faltan tras una concesión parcial`() {
        val missing = HealthPermissions.missing(HealthDataType.entries, granted = setOf(heartRate))
        assertEquals(setOf(HealthDataType.STEPS, HealthDataType.SLEEP, HealthDataType.ACTIVE_CALORIES), missing)
    }

    @Test fun `nada falta si todo está concedido`() {
        assertTrue(HealthPermissions.missing(HealthDataType.entries, all).isEmpty())
    }
}
