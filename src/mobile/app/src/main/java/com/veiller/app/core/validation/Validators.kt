package com.veiller.app.core.validation

import com.veiller.app.core.model.ConsentItem

object FormValidators {
    private val EmailRegex = Regex("^[^\\s@]+@[^\\s@]+\\.[^\\s@]{2,}$")
    const val MIN_PASSWORD_LENGTH = 8

    fun nameError(name: String): String? =
        if (name.isBlank()) "Ingresa tu nombre completo" else null

    fun emailError(email: String): String? =
        if (!EmailRegex.matches(email.trim())) "Ingresa un correo válido" else null

    fun passwordError(password: String): String? =
        if (password.length < MIN_PASSWORD_LENGTH) "Debe tener mínimo $MIN_PASSWORD_LENGTH caracteres" else null
}

/** Códigos de círculo con formato VLR-XXXX. */
object CircleCode {
    private const val PREFIX = "VLR"
    private const val BODY_LENGTH = 4
    private val ValidRegex = Regex("^$PREFIX-[A-Z0-9]{$BODY_LENGTH}$")

    /**
     * Normaliza lo que escribe el usuario: mayúsculas, sin símbolos y con el
     * prefijo y guion puestos automáticamente.
     */
    fun format(input: String): String {
        val raw = input.uppercase().filter { it in 'A'..'Z' || it in '0'..'9' }
        if (raw.length <= PREFIX.length && PREFIX.startsWith(raw)) return raw
        val body = if (raw.startsWith(PREFIX)) raw.drop(PREFIX.length) else raw
        return "$PREFIX-" + body.take(BODY_LENGTH)
    }

    fun isValid(code: String): Boolean = ValidRegex.matches(code)
}

object ConsentRules {
    fun canAccept(items: List<ConsentItem>, disclaimerAccepted: Boolean): Boolean =
        disclaimerAccepted && items.filter { it.type.required }.all { it.granted }
}
