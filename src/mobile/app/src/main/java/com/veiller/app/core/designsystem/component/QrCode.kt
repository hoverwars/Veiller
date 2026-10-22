package com.veiller.app.core.designsystem.component

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.qrcode.QRCodeWriter
import com.veiller.app.core.designsystem.theme.VeillerColors

/** Dibuja un código QR generado localmente con ZXing. */
@Composable
fun QrCode(content: String, modifier: Modifier = Modifier, size: Dp = 140.dp, color: Color = VeillerColors.TextPrimary) {
    val matrix = remember(content) {
        QRCodeWriter().encode(content, BarcodeFormat.QR_CODE, 0, 0, mapOf(EncodeHintType.MARGIN to 0))
    }
    Canvas(modifier.size(size).semantics { contentDescription = "Código QR" }) {
        val cell = this.size.width / matrix.width
        for (x in 0 until matrix.width) {
            for (y in 0 until matrix.height) {
                if (matrix[x, y]) {
                    drawRect(color, topLeft = Offset(x * cell, y * cell), size = Size(cell + 0.5f, cell + 0.5f))
                }
            }
        }
    }
}
