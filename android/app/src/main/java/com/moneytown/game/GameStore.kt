package com.moneytown.game

import android.content.Context
import android.util.AtomicFile
import java.io.File

class GameStore(context: Context) {
    private val path = File(context.filesDir, "game-v1.bin")
    private val file = AtomicFile(path)
    fun load(): GameState? {
        if (!path.exists() && !File(path.path + ".bak").exists()) return null
        return file.openRead().use { stream ->
            val bytes = ByteArray(65537)
            var count = 0
            while (count < bytes.size) {
                val read = stream.read(bytes, count, bytes.size - count)
                if (read < 0) break
                count += read
            }
            SaveCodec.decode(bytes.copyOf(count))
        }
    }
    fun save(state: GameState) {
        val bytes = SaveCodec.encode(state)
        val stream = file.startWrite()
        try {
            stream.write(bytes)
            file.finishWrite(stream)
        } catch (error: Exception) {
            file.failWrite(stream)
            throw error
        }
    }
    fun reset(): GameState {
        // 손상된 저장도 확인 후 복사본을 남겨 수동 복구할 수 있게 합니다.
        if (path.exists()) path.copyTo(File(path.parentFile, "game-before-reset-${System.currentTimeMillis()}.bin"))
        val state = Market.create()
        save(state)
        return state
    }
}
