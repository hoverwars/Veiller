package com.veiller.app.data.health

import com.veiller.app.core.model.HealthDataType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class HealthPermissionsTest {
    private val heartRate = "android.permission.health.READ_HEART_RATE"
    private val steps = "android.permission.health.READ_STEPS"
    private val sleep = "android.permission.health.READ_SLEEP"

    @Test fun `los tipos de F1 piden su permiso de lectura`() {
        assertEquals(setOf(heartRate), HealthPermissions.forType(HealthDataType.HEART_RATE))
        assertEquals(setOf(steps), HealthPermissions.forType(HealthDataType.STEPS))
        assertEquals(setOf(sleep), HealthPermissions.forType(HealthDataType.SLEEP))
    }

    @Test fun `los tipos que no vienen de Health Connect no piden nada`() {
        listOf(HealthDataType.LOCATION, HealthDataType.SCREEN_TIME, HealthDataType.ECG).forEach {
            assertTrue(HealthPermissions.forType(it).isEmpty())
        }
    }

    @Test fun `todos los obligatorios de Health Connect quedan cubiertos`() {
        assertEquals(setOf(heartRate, steps, sleep), HealthPermissions.forTypes(HealthDataType.entries))
    }

    @Test fun `detecta los permisos que faltan tras una concesión parcial`() {
        val missing = HealthPermissions.missing(HealthDataType.entries, granted = setOf(heartRate))
        assertEquals(setOf(HealthDataType.STEPS, HealthDataType.SLEEP), missing)
    }

    @Test fun `nada falta si todo está concedido`() {
        assertTrue(HealthPermissions.missing(HealthDataType.entries, setOf(heartRate, steps, sleep)).isEmpty())
    }
}
