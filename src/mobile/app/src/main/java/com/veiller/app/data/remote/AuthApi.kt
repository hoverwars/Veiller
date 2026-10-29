package com.veiller.app.data.remote

import kotlinx.serialization.Serializable
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST

/** Endpoints /api/auth del backend. Las rutas son relativas a BuildConfig.API_BASE_URL. */
interface AuthApi {
    @POST("auth/register")
    suspend fun register(@Body body: RegisterBody): AuthResponseDto

    @POST("auth/login")
    suspend fun login(@Body body: LoginBody): AuthResponseDto

    @POST("auth/refresh")
    suspend fun refresh(@Body body: RefreshBody): TokenResponseDto

    @GET("auth/me")
    suspend fun me(@Header("Authorization") bearer: String): UserDto

    @POST("auth/me/onboarding")
    suspend fun completeOnboarding(@Header("Authorization") bearer: String)

    @POST("auth/logout")
    suspend fun logout(@Header("Authorization") bearer: String)
}

@Serializable
data class RegisterBody(val fullName: String, val email: String, val password: String, val role: String)

@Serializable
data class LoginBody(val email: String, val password: String)

@Serializable
data class RefreshBody(val refreshToken: String)

@Serializable
data class UserDto(
    val id: String,
    val fullName: String,
    val email: String,
    val role: String,
    val onboardingCompleted: Boolean,
)

@Serializable
data class AuthResponseDto(val user: UserDto, val idToken: String, val refreshToken: String, val expiresIn: Int)

@Serializable
data class TokenResponseDto(val idToken: String, val refreshToken: String, val expiresIn: Int)
