package com.veiller.app.data.fake

import com.veiller.app.core.model.Circle
import com.veiller.app.core.model.CircleMember
import com.veiller.app.core.model.CirclePreview
import com.veiller.app.core.model.InviteCode
import com.veiller.app.core.model.MemberRole
import com.veiller.app.core.validation.CircleCode
import com.veiller.app.data.repository.AppError
import com.veiller.app.data.repository.CircleRepository
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.update
import java.time.Duration
import java.time.Instant

class FakeCircleRepository : CircleRepository {

    private val _circles = MutableStateFlow(FakeData.circles.associateBy { it.id })
    val circles: StateFlow<Map<String, Circle>> = _circles

    private val codes = mutableMapOf(FakeData.ROSA_CODE to FakeData.ROSA_CIRCLE)
    private var firstCodeServed = false

    override val ownCircleId: String = FakeData.ROSA_CIRCLE

    override fun observeCircle(circleId: String): Flow<Circle?> = _circles.map { it[circleId] }

    override suspend fun generateInviteCode(circleId: String): InviteCode {
        delay(NETWORK_DELAY_MS)
        val code = if (!firstCodeServed && circleId == FakeData.ROSA_CIRCLE) {
            firstCodeServed = true
            FakeData.ROSA_CODE
        } else {
            "VLR-" + (1..4).map { CODE_ALPHABET.random() }.joinToString("")
        }
        codes[code] = circleId
        return InviteCode(code, Instant.now().plus(Duration.ofHours(23).plusMinutes(58)))
    }

    override suspend fun findByCode(code: String): Result<CirclePreview> {
        delay(NETWORK_DELAY_MS)
        val circle = codes[CircleCode.format(code)]?.let { _circles.value[it] }
            ?: return Result.failure(AppError("No encontramos un círculo con ese código. Revisa que esté bien escrito."))
        return Result.success(
            CirclePreview(
                code = code,
                ownerName = circle.owner.fullName,
                ownerAge = circle.ownerAge,
                adminName = circle.admin?.fullName,
                plan = circle.plan,
                caregiverCount = circle.caregivers.size,
            ),
        )
    }

    override suspend fun requestToJoin(code: String): Result<Unit> =
        findByCode(code).map { }

    override suspend fun approveRequest(circleId: String, requestId: String): Result<Unit> {
        delay(NETWORK_DELAY_MS)
        val circle = _circles.value[circleId] ?: return Result.failure(AppError("Círculo no encontrado"))
        val request = circle.pendingRequests.firstOrNull { it.id == requestId }
            ?: return Result.failure(AppError("La solicitud ya no existe"))
        if (circle.isFull) {
            return Result.failure(AppError("Límite del plan alcanzado. Mejora a Premium o retira a un cuidador."))
        }
        val member = CircleMember(userId = "u-${request.id}", fullName = request.fullName, role = MemberRole.CAREGIVER)
        updateCircle(circleId) {
            it.copy(members = it.members + member, pendingRequests = it.pendingRequests - request)
        }
        return Result.success(Unit)
    }

    override suspend fun rejectRequest(circleId: String, requestId: String) {
        delay(NETWORK_DELAY_MS)
        updateCircle(circleId) { c -> c.copy(pendingRequests = c.pendingRequests.filterNot { it.id == requestId }) }
    }

    override suspend fun removeMember(circleId: String, userId: String) {
        delay(NETWORK_DELAY_MS)
        updateCircle(circleId) { c ->
            c.copy(members = c.members.filterNot { it.userId == userId && it.role != MemberRole.DATA_OWNER })
        }
    }

    private fun updateCircle(circleId: String, transform: (Circle) -> Circle) {
        _circles.update { all ->
            val circle = all[circleId] ?: return@update all
            all + (circleId to transform(circle))
        }
    }

    private companion object {
        const val CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"
    }
}
