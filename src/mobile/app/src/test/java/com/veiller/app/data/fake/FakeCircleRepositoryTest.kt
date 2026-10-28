package com.veiller.app.data.fake

import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class FakeCircleRepositoryTest {
    private val repo = FakeCircleRepository()

    @Test fun `el código de los mockups encuentra el círculo de Rosa`() = runBlocking {
        val preview = repo.findByCode("VLR-7K2Q").getOrThrow()
        assertEquals("Rosa Martínez", preview.ownerName)
        assertEquals("Laura Gómez", preview.adminName)
    }

    @Test fun `un código desconocido falla`() = runBlocking {
        assertTrue(repo.findByCode("VLR-0000").isFailure)
    }

    @Test fun `un código generado se puede usar para unirse`() = runBlocking {
        val invite = repo.generateInviteCode(FakeData.ROSA_CIRCLE)
        assertTrue(repo.findByCode(invite.code).isSuccess)
    }

    @Test fun `no se aprueba una solicitud con el plan lleno`() = runBlocking {
        val result = repo.approveRequest(FakeData.ROSA_CIRCLE, "req-pedro")
        assertTrue(result.isFailure)
    }

    @Test fun `retirar a un cuidador libera cupo para aprobar`() = runBlocking {
        repo.removeMember(FakeData.ROSA_CIRCLE, "u-juan")
        assertTrue(repo.approveRequest(FakeData.ROSA_CIRCLE, "req-pedro").isSuccess)
        val circle = repo.observeCircle(FakeData.ROSA_CIRCLE).first()!!
        assertEquals(listOf("Laura Gómez", "Pedro Ruiz"), circle.caregivers.map { it.fullName })
        assertTrue(circle.pendingRequests.isEmpty())
    }

    @Test fun `el titular no se puede retirar`() = runBlocking {
        repo.removeMember(FakeData.ROSA_CIRCLE, FakeData.ROSA_ID)
        val circle = repo.observeCircle(FakeData.ROSA_CIRCLE).first()!!
        assertEquals(FakeData.ROSA_ID, circle.owner.userId)
        assertTrue(circle.members.any { it.userId == FakeData.ROSA_ID })
    }
}
