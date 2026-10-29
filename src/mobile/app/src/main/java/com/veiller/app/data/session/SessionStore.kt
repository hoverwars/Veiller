package com.veiller.app.data.session

import android.content.Context
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.veiller.app.core.model.Role
import com.veiller.app.core.model.Session
import com.veiller.app.core.model.User
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.sessionDataStore by preferencesDataStore(name = "session")

/** Tokens de Firebase devueltos por la API. [expiresAtMs] es la hora de expiración del idToken. */
data class AuthTokens(val idToken: String, val refreshToken: String, val expiresAtMs: Long)

/** Persiste la sesión actual (usuario y tokens) para reabrir la app sin volver a iniciar sesión. */
class SessionStore(private val context: Context) {

    val session: Flow<Session?> = context.sessionDataStore.data.map { prefs ->
        prefs.readUser()?.let { user ->
            Session(user, prefs[ONBOARDING_COMPLETED] ?: false)
        }
    }

    val tokens: Flow<AuthTokens?> = context.sessionDataStore.data.map { prefs ->
        val idToken = prefs[ID_TOKEN] ?: return@map null
        val refreshToken = prefs[REFRESH_TOKEN] ?: return@map null
        AuthTokens(idToken, refreshToken, prefs[EXPIRES_AT] ?: 0L)
    }

    suspend fun saveSession(user: User, onboardingCompleted: Boolean, tokens: AuthTokens) {
        context.sessionDataStore.edit { prefs ->
            prefs[USER_PREFIX + "id"] = user.id
            prefs[USER_PREFIX + "name"] = user.fullName
            prefs[USER_PREFIX + "email"] = user.email
            prefs[USER_PREFIX + "role"] = user.role.name
            prefs[ONBOARDING_COMPLETED] = onboardingCompleted
            prefs.writeTokens(tokens)
        }
    }

    suspend fun saveTokens(tokens: AuthTokens) {
        context.sessionDataStore.edit { it.writeTokens(tokens) }
    }

    suspend fun setOnboardingCompleted() {
        context.sessionDataStore.edit { it[ONBOARDING_COMPLETED] = true }
    }

    suspend fun clearSession() {
        context.sessionDataStore.edit { it.clear() }
    }

    private fun androidx.datastore.preferences.core.MutablePreferences.writeTokens(tokens: AuthTokens) {
        this[ID_TOKEN] = tokens.idToken
        this[REFRESH_TOKEN] = tokens.refreshToken
        this[EXPIRES_AT] = tokens.expiresAtMs
    }

    private fun Preferences.readUser(): User? {
        val id = this[USER_PREFIX + "id"] ?: return null
        return User(
            id = id,
            fullName = this[USER_PREFIX + "name"].orEmpty(),
            email = this[USER_PREFIX + "email"].orEmpty(),
            role = Role.valueOf(this[USER_PREFIX + "role"] ?: Role.CAREGIVER.name),
        )
    }

    private operator fun Preferences.get(name: String): String? = this[stringPreferencesKey(name)]

    private operator fun androidx.datastore.preferences.core.MutablePreferences.set(name: String, value: String) {
        this[stringPreferencesKey(name)] = value
    }

    private companion object {
        const val USER_PREFIX = "session_"
        val ONBOARDING_COMPLETED = booleanPreferencesKey("onboarding_completed")
        val ID_TOKEN = stringPreferencesKey("id_token")
        val REFRESH_TOKEN = stringPreferencesKey("refresh_token")
        val EXPIRES_AT = longPreferencesKey("expires_at_ms")
    }
}
