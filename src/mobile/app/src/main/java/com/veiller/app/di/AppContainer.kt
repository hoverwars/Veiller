package com.veiller.app.di

import android.content.Context
import com.veiller.app.data.fake.FakeAuthRepository
import com.veiller.app.data.fake.FakeCircleRepository
import com.veiller.app.data.fake.FakeConsentRepository
import com.veiller.app.data.fake.FakeElderRepository
import com.veiller.app.data.fake.FakePatientRepository
import com.veiller.app.data.repository.AuthRepository
import com.veiller.app.data.repository.CircleRepository
import com.veiller.app.data.repository.ConsentRepository
import com.veiller.app.data.repository.ElderRepository
import com.veiller.app.data.repository.PatientRepository
import com.veiller.app.data.session.SessionStore

/**
 * Inyección de dependencias manual. Para conectar el backend, reemplazar
 * aquí cada Fake* por su implementación Retrofit.
 */
class AppContainer(context: Context) {
    private val sessionStore = SessionStore(context.applicationContext)
    private val fakeCircles = FakeCircleRepository()

    val authRepository: AuthRepository = FakeAuthRepository(sessionStore)
    val circleRepository: CircleRepository = fakeCircles
    val patientRepository: PatientRepository = FakePatientRepository(fakeCircles)
    val consentRepository: ConsentRepository = FakeConsentRepository()
    val elderRepository: ElderRepository = FakeElderRepository(fakeCircles)
}
