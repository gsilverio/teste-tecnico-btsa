import axios from 'axios'

// VITE_API_BASE_URL is the complete API base path (for example `/api`).
// Keeping the prefix in one place prevents Docker builds from producing `/api/api`.
const baseUrl = (import.meta.env.VITE_API_BASE_URL?.trim() || '/api').replace(/\/$/, '')
export const swaggerUrl = `${baseUrl.replace(/\/api$/, '')}/swagger`

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ProblemDetails {
  detail?: string
  title?: string
}

export const http = axios.create({
  baseURL: baseUrl,
  timeout: 15_000,
  headers: { Accept: 'application/json' },
})

http.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError<ProblemDetails>(error)) {
      const message = error.response?.data?.detail
        ?? error.response?.data?.title
        ?? (error.response ? `A solicitação falhou (HTTP ${error.response.status}).` : 'Não foi possível conectar à API. Confira o backend e tente novamente.')
      return Promise.reject(new ApiError(message, error.response?.status ?? 0))
    }
    return Promise.reject(error)
  },
)
