package com.veiller.app.core.ui

import com.veiller.app.core.model.initialsOf
import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.Duration
import java.time.Instant
import java.time.ZoneId

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

    @Test fun `antigüedad de una medición`() {
        val now = Instant.parse("2026-11-01T22:30:00Z")
        assertEquals("hace un momento", Format.agoSince(now.minusSeconds(30), now))
        assertEquals("hace 8 min", Format.agoSince(now.minusSeconds(8 * 60), now))
        assertEquals("hace 8 h", Format.agoSince(now.minusSeconds(8 * 3600), now))
    }

    @Test fun `hora real de la medición`() {
        val zone = ZoneId.of("America/Bogota")
        val now = Instant.parse("2026-11-01T22:30:00Z") // 17:30 en Bogotá
        assertEquals("08:57", Format.clockTime(Instant.parse("2026-11-01T13:57:00Z"), now, zone))
        assertEquals("ayer 23:10", Format.clockTime(Instant.parse("2026-11-01T04:10:00Z"), now, zone))
        assertEquals("30/10 07:00", Format.clockTime(Instant.parse("2026-10-30T12:00:00Z"), now, zone))
        assertEquals("08:57 · hace 8 h", Format.measuredAt(Instant.parse("2026-11-01T13:57:00Z"), now, zone))
    }

    @Test fun conteo() {
        assertEquals("8.894", Format.count(8894))
    }

    @Test fun cuidadores() {
        assertEquals("1 cuidador", Format.caregivers(1))
        assertEquals("2 cuidadores", Format.caregivers(2))
    }

    @Test fun `vencimiento del código`() {
        assertEquals("Vence en 23 h 58 min", Format.remaining(Duration.ofHours(23).plusMinutes(58)))
        assertEquals("Código vencido", Format.remaining(Duration.ofMinutes(-1)))
    }

    @Test fun iniciales() {
        assertEquals("RM", initialsOf("Rosa Martínez"))
        assertEquals("LG", initialsOf("  laura   gómez pérez "))
        assertEquals("P", initialsOf("Pedro"))
    }
}
