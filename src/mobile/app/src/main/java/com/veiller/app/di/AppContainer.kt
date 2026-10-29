package com.veiller.app.di

import android.content.Context
import com.veiller.app.data.fake.FakeCircleRepository
import com.veiller.app.data.fake.FakeConsentRepository
import com.veiller.app.data.fake.FakeElderRepository
import com.veiller.app.data.fake.FakePatientRepository
import com.veiller.app.data.remote.ApiAuthRepository
import com.veiller.app.data.remote.ApiClient
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.repository.CircleRepository
import com.veiller.app.data.repository.ConsentRepository
import com.veiller.app.data.repository.ElderRepository
import com.veiller.app.data.repository.PatientRepository
import com.veiller.app.data.session.SessionStore

/**
 * Inyección de dependencias manual. La autenticación ya usa el backend; el resto
 * de repositorios sigue con Fake* hasta que existan sus endpoints (reemplazar aquí).
 */
class AppContainer(context: Context) {
    private val sessionStore = SessionStore(context.applicationContext)
    private val fakeCircles = FakeCircleRepository()

    val authRepository: AuthRepository = ApiAuthRepository(ApiClient.authApi, sessionStore)
    val circleRepository: CircleRepository = fakeCircles
    val patientRepository: PatientRepository = FakePatientRepository(fakeCircles)
    val consentRepository: ConsentRepository = FakeConsentRepository()
    val elderRepository: ElderRepository = FakeElderRepository(fakeCircles)
}
