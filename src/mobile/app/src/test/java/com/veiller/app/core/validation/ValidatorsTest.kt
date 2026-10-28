package com.veiller.app.core.validation

import com.veiller.app.core.model.ConsentItem
import com.veiller.app.core.model.HealthDataType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class FormValidatorsTest {
    @Test fun `nombre vacío es inválido`() {
        assertNotNull(FormValidators.nameError("   "))
        assertNull(FormValidators.nameError("Laura Gómez"))
    }

    @Test fun `correo necesita usuario dominio y extensión`() {
        assertNull(FormValidators.emailError("laura@correo.com"))
        assertNull(FormValidators.emailError("  laura@correo.com "))
        assertNotNull(FormValidators.emailError("laura@correo"))
        assertNotNull(FormValidators.emailError("laura.correo.com"))
        assertNotNull(FormValidators.emailError(""))
    }

    @Test fun `contraseña de mínimo 8 caracteres`() {
        assertNotNull(FormValidators.passwordError("1234567"))
        assertNull(FormValidators.passwordError("12345678"))
    }
}

class CircleCodeTest {
    @Test fun `agrega prefijo y guion al escribir el cuerpo`() {
        assertEquals("VLR-7K2Q", CircleCode.format("7k2q"))
        assertEquals("VLR-7K2Q", CircleCode.format("vlr7k2q"))
        assertEquals("VLR-7K2Q", CircleCode.format("VLR-7K2Q"))
    }

    @Test fun `respeta el prefijo mientras se escribe`() {
        assertEquals("V", CircleCode.format("v"))
        assertEquals("VL", CircleCode.format("vl"))
        assertEquals("VLR", CircleCode.format("VLR-"))
        assertEquals("VLR-7", CircleCode.format("VLR-7"))
    }

    @Test fun `ignora símbolos y recorta a 4 caracteres`() {
        assertEquals("VLR-7K2Q", CircleCode.format("VLR-7K2Q99"))
        assertEquals("VLR-7K2Q", CircleCode.format(" 7-k 2_q "))
    }

    @Test fun `validación de formato`() {
        assertTrue(CircleCode.isValid("VLR-7K2Q"))
        assertFalse(CircleCode.isValid("VLR-7K2"))
        assertFalse(CircleCode.isValid("ABC-7K2Q"))
        assertFalse(CircleCode.isValid("vlr-7k2q"))
    }
}

class ConsentRulesTest {
    private val allGranted = HealthDataType.entries.map { ConsentItem(it, granted = true) }

    @Test fun `requiere aceptar el aviso`() {
        assertFalse(ConsentRules.canAccept(allGranted, disclaimerAccepted = false))
        assertTrue(ConsentRules.canAccept(allGranted, disclaimerAccepted = true))
    }

    @Test fun `los opcionales pueden estar desactivados`() {
        val items = allGranted.map { if (it.type.required) it else it.copy(granted = false) }
        assertTrue(ConsentRules.canAccept(items, disclaimerAccepted = true))
    }

    @Test fun `un permiso necesario desactivado bloquea`() {
        val items = allGranted.map { if (it.type == HealthDataType.LOCATION) it.copy(granted = false) else it }
        assertFalse(ConsentRules.canAccept(items, disclaimerAccepted = true))
    }
}
