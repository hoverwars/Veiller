package com.veiller.app.data.fake

import com.veiller.app.core.model.Role
import com.veiller.app.core.model.Session
import com.veiller.app.core.model.User
import com.veiller.app.core.validation.FormValidators
import com.veiller.app.data.repository.AppError
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.session.SessionStore
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first

class FakeAuthRepository(private val store: SessionStore) : AuthRepository {

    override val session: Flow<Session?> = store.session

    override suspend fun register(fullName: String, email: String, password: String, role: Role): Result<User> {
        delay(NETWORK_DELAY_MS)
        // Se reutilizan los ids de Laura/Rosa para que los datos de ejemplo
        // (miembros, "tú", círculo propio) sigan siendo coherentes.
        val id = if (role == Role.CAREGIVER) FakeData.LAURA_ID else FakeData.ROSA_ID
        val user = User(id, fullName.trim(), email.trim().lowercase(), role)
        store.saveRegisteredUser(user)
        store.saveSession(user, onboardingCompleted = false)
        return Result.success(user)
    }

    override suspend fun login(email: String, password: String): Result<User> {
        delay(NETWORK_DELAY_MS)
        val normalized = email.trim().lowercase()
        val user = (listOfNotNull(store.registeredUser.first()) + FakeData.demoUsers)
            .firstOrNull { it.email == normalized }
        if (user == null || FormValidators.passwordError(password) != null) {
            return Result.failure(AppError("Correo o contraseña incorrectos"))
        }
        store.saveSession(user, onboardingCompleted = true)
        return Result.success(user)
    }

    override suspend fun completeOnboarding() = store.setOnboardingCompleted()

    override suspend fun logout() = store.clearSession()
}

internal const val NETWORK_DELAY_MS = 400L
