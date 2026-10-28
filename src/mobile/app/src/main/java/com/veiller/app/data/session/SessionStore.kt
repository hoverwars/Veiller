package com.veiller.app.data.session

import android.content.Context
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.veiller.app.core.model.Role
import com.veiller.app.core.model.Session
import com.veiller.app.core.model.User
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.sessionDataStore by preferencesDataStore(name = "session")

/** Persiste la sesión actual y la última cuenta registrada (solo para el modo sin backend). */
class SessionStore(private val context: Context) {

    val session: Flow<Session?> = context.sessionDataStore.data.map { prefs ->
        prefs.readUser(SESSION_PREFIX)?.let { user ->
            Session(user, prefs[ONBOARDING_COMPLETED] ?: false)
        }
    }

    val registeredUser: Flow<User?> = context.sessionDataStore.data.map { it.readUser(REGISTERED_PREFIX) }

    suspend fun saveSession(user: User, onboardingCompleted: Boolean) {
        context.sessionDataStore.edit { prefs ->
            prefs.writeUser(SESSION_PREFIX, user)
            prefs[ONBOARDING_COMPLETED] = onboardingCompleted
        }
    }

    suspend fun saveRegisteredUser(user: User) {
        context.sessionDataStore.edit { it.writeUser(REGISTERED_PREFIX, user) }
    }

    suspend fun setOnboardingCompleted() {
        context.sessionDataStore.edit { it[ONBOARDING_COMPLETED] = true }
    }

    suspend fun clearSession() {
        context.sessionDataStore.edit { prefs ->
            USER_FIELDS.forEach { prefs.remove(stringPreferencesKey(SESSION_PREFIX + it)) }
            prefs.remove(ONBOARDING_COMPLETED)
        }
    }

    private fun Preferences.readUser(prefix: String): User? {
        val id = this[stringPreferencesKey(prefix + "id")] ?: return null
        return User(
            id = id,
            fullName = this[stringPreferencesKey(prefix + "name")].orEmpty(),
            email = this[stringPreferencesKey(prefix + "email")].orEmpty(),
            role = Role.valueOf(this[stringPreferencesKey(prefix + "role")] ?: Role.CAREGIVER.name),
        )
    }

    private fun androidx.datastore.preferences.core.MutablePreferences.writeUser(prefix: String, user: User) {
        this[stringPreferencesKey(prefix + "id")] = user.id
        this[stringPreferencesKey(prefix + "name")] = user.fullName
        this[stringPreferencesKey(prefix + "email")] = user.email
        this[stringPreferencesKey(prefix + "role")] = user.role.name
    }

    private companion object {
        const val SESSION_PREFIX = "session_"
        const val REGISTERED_PREFIX = "registered_"
        val USER_FIELDS = listOf("id", "name", "email", "role")
        val ONBOARDING_COMPLETED = booleanPreferencesKey("onboarding_completed")
    }
}
