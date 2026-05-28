import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 10000,
  withCredentials: true,
})

api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      const publicRoutes = ['/login', '/register', '/forgot-password']
      if (!publicRoutes.includes(window.location.pathname)) {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

export const sendCode = (email) => api.post('/auth/send-code', { email })
export const register = (data) => api.post('/auth/register', data)
export const login = (data) => api.post('/auth/login', data)
export const logout = () => api.post('/auth/logout')
export const forgotPassword = (email) => api.post('/auth/forgot-password', { email })
export const resetPassword = (data) => api.post('/auth/reset-password', data)
export const getCurrentUser = () => api.get('/auth/me')
export const checkRouteAccess = (path) => api.post('/auth/check-route-access', { path })

export const getUsers = () => api.get('/users')
export const getUser = (id) => api.get(`/users/${id}`)
export const createUser = (data) => api.post('/users', data)

export const getForums = () => api.get('/forums')
export const getForum = (id) => api.get(`/forums/${id}`)

export const getPosts = () => api.get('/posts')
export const getPost = (id) => api.get(`/posts/${id}`)

export const getProducts = () => api.get('/products')
export const getProduct = (id) => api.get(`/products/${id}`)

export const getHealth = () => api.get('/health')

export default api
