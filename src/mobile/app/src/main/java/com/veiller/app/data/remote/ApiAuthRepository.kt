package com.veiller.app.data.remote

import com.veiller.app.core.model.Role
import com.veiller.app.core.model.Session
import com.veiller.app.core.model.User
import com.veiller.app.data.repository.AppError
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.session.AuthTokens
import com.veiller.app.data.session.SessionStore
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import retrofit2.HttpException
import java.io.IOException

/** Implementación de [AuthRepository] sobre la API /api/auth (Firebase Auth detrás). */
class ApiAuthRepository(
    private val api: AuthApi,
    private val store: SessionStore,
) : AuthRepository {

    override val session: Flow<Session?> = store.session

    override suspend fun register(fullName: String, email: String, password: String, role: Role): Result<User> =
        call(unauthorizedMessage = "No se pudo crear la cuenta") {
            val response = api.register(RegisterBody(fullName.trim(), email.trim(), password, role.wire()))
            saveSession(response)
        }

    override suspend fun login(email: String, password: String): Result<User> =
        call(unauthorizedMessage = "Correo o contraseña incorrectos") {
            val response = api.login(LoginBody(email.trim(), password))
            saveSession(response)
        }

    override suspend fun completeOnboarding() {
        // El onboarding se marca localmente aunque el servidor no responda; se reintenta al reabrir la app.
        store.setOnboardingCompleted()
        runCatching { api.completeOnboarding(bearer() ?: return) }
    }

    override suspend fun logout() {
        runCatching { api.logout(bearer() ?: return@runCatching) }
        store.clearSession()
    }

    private suspend fun saveSession(response: AuthResponseDto): User {
        val user = response.user.toUser()
        store.saveSession(user, response.user.onboardingCompleted, response.toTokens())
        return user
    }

    /** Devuelve "Bearer <idToken>", renovándolo si está por expirar; null si no hay sesión o no se pudo renovar. */
    private suspend fun bearer(): String? {
        val tokens = store.tokens.first() ?: return null
        if (System.currentTimeMillis() < tokens.expiresAtMs - REFRESH_MARGIN_MS) return "Bearer ${tokens.idToken}"
        return try {
            val fresh = api.refresh(RefreshBody(tokens.refreshToken))
            val renewed = AuthTokens(fresh.idToken, fresh.refreshToken, expiresAt(fresh.expiresIn))
            store.saveTokens(renewed)
            "Bearer ${renewed.idToken}"
        } catch (e: HttpException) {
            // Refresh token inválido o revocado: la sesión ya no sirve.
            if (e.code() == 401) store.clearSession()
            null
        } catch (e: IOException) {
            null
        }
    }

    private suspend fun call(unauthorizedMessage: String, block: suspend () -> User): Result<User> =
        try {
            Result.success(block())
        } catch (e: CancellationException) {
            throw e
        } catch (e: HttpException) {
            Result.failure(AppError(messageFor(e.code(), unauthorizedMessage)))
        } catch (e: IOException) {
            Result.failure(AppError("No se pudo conectar con el servidor. Revisa tu conexión"))
        }

    private fun messageFor(code: Int, unauthorizedMessage: String) = when (code) {
        400 -> "Revisa los datos ingresados"
        401 -> unauthorizedMessage
        409 -> "Ya existe una cuenta con este correo"
        429 -> "Demasiados intentos. Inténtalo más tarde"
        else -> "El servidor no está disponible. Inténtalo más tarde"
    }

    private fun AuthResponseDto.toTokens() = AuthTokens(idToken, refreshToken, expiresAt(expiresIn))

    private fun UserDto.toUser() = User(id, fullName, email, Role.valueOf(role.uppercase()))

    private fun Role.wire() = name.lowercase()

    private fun expiresAt(expiresInSeconds: Int) = System.currentTimeMillis() + expiresInSeconds * 1000L

    private companion object {
        const val REFRESH_MARGIN_MS = 60_000L
    }
}
