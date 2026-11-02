package com.veiller.app.data.repository

import com.veiller.app.core.model.Circle
import com.veiller.app.core.model.CirclePreview
import com.veiller.app.core.model.ConsentItem
import com.veiller.app.core.model.ElderHome
import com.veiller.app.core.model.HealthDataType
import com.veiller.app.core.model.HistoryRange
import com.veiller.app.core.model.InviteCode
import com.veiller.app.core.model.PatientHistory
import com.veiller.app.core.model.PatientSummary
import com.veiller.app.core.model.Role
import com.veiller.app.core.model.Session
import com.veiller.app.core.model.User
import com.veiller.app.data.health.HealthAvailability
import com.veiller.app.data.health.HealthSnapshot
import kotlinx.coroutines.flow.Flow

/*
 * Contratos que la app espera del backend. Hoy los implementan los Fake* de
 * data/fake; cuando existan endpoints basta con crear la versión Retrofit y
 * cambiarla en AppContainer.
 */

interface AuthRepository {
    /** Emite null cuando no hay sesión iniciada. */
    val session: Flow<Session?>

    suspend fun register(fullName: String, email: String, password: String, role: Role): Result<User>
    suspend fun login(email: String, password: String): Result<User>
    suspend fun completeOnboarding()
    suspend fun logout()
}

interface ConsentRepository {
    /** Consentimientos del adulto mayor dueño del círculo indicado, o del usuario actual si es null. */
    suspend fun getConsents(circleId: String? = null): List<ConsentItem>
    suspend fun saveConsents(items: List<ConsentItem>)
}

interface CircleRepository {
    /** Círculo del adulto mayor con la sesión iniciada. */
    val ownCircleId: String

    fun observeCircle(circleId: String): Flow<Circle?>
    suspend fun generateInviteCode(circleId: String): InviteCode
    suspend fun findByCode(code: String): Result<CirclePreview>
    suspend fun requestToJoin(code: String): Result<Unit>
    suspend fun approveRequest(circleId: String, requestId: String): Result<Unit>
    suspend fun rejectRequest(circleId: String, requestId: String)
    suspend fun removeMember(circleId: String, userId: String)
}

interface PatientRepository {
    fun observePatients(): Flow<List<PatientSummary>>
    suspend fun getPatient(circleId: String): PatientSummary?
    suspend fun getHistory(circleId: String, range: HistoryRange): PatientHistory
}

interface ElderRepository {
    suspend fun getHome(): ElderHome
}

/**
 * Datos de salud leídos en el teléfono del adulto mayor (Health Connect), no del
 * backend. Solo se usa con el rol ELDER.
 */
interface HealthRepository {
    fun availability(): HealthAvailability

    /** Permisos de Health Connect que hay que pedir para leer estos tipos. */
    fun permissionsFor(types: Collection<HealthDataType>): Set<String>

    /** Tipos (de los que se leen por Health Connect) cuyo permiso no está concedido. */
    suspend fun missingPermissions(types: Collection<HealthDataType>): Set<HealthDataType>

    suspend fun readToday(): HealthSnapshot
}

class AppError(message: String) : Exception(message)
