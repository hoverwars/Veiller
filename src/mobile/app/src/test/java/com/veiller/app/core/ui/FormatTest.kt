package com.veiller.app.core.ui

import com.veiller.app.core.model.initialsOf
import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.Duration

class FormatTest {
    @Test fun sueño() {
        assertEquals("5 h 10", Format.sleep(310))
        assertEquals("7 h 05", Format.sleep(425))
        assertEquals("—", Format.sleep(null))
        assertEquals("5:10", Format.sleepClock(310))
    }

    @Test fun `tiempo transcurrido`() {
        assertEquals("8 min", Format.ago(8))
        assertEquals("3 h", Format.ago(180))
        assertEquals("2 d", Format.ago(60 * 48))
    }

    @Test fun cuidadores() {
        assertEquals("1 cuidador", Format.caregivers(1))
        assertEquals("2 cuidadores", Format.caregivers(2))
    }

    @Test fun `vencimiento del código`() {
        assertEquals("Vence en 23 h 58 min", Format.remaining(Duration.ofHours(23).plusMinutes(58)))
        assertEquals("Código vencido", Format.remaining(Duration.ofMinutes(-1)))
        assertEquals("Código vencido", Format.remaining(Duration.ZERO))
    }

    @Test fun `vencimiento ignora segundos y admite más de un día`() {
        assertEquals("Vence en 1 h 5 min", Format.remaining(Duration.ofHours(1).plusMinutes(5).plusSeconds(59)))
        assertEquals("Vence en 0 h 0 min", Format.remaining(Duration.ofSeconds(30)))
        assertEquals("Vence en 25 h 0 min", Format.remaining(Duration.ofHours(25)))
    }

    @Test fun iniciales() {
        assertEquals("RM", initialsOf("Rosa Martínez"))
        assertEquals("LG", initialsOf("  laura   gómez pérez "))
        assertEquals("P", initialsOf("Pedro"))
    }
}
