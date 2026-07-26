export const ROUTE_ACCESS_RULES = {
  '/': [],
  '/forums': ['forums.view', 'posts.view'],
  '/products': ['products.view'],
  '/messages': [],
  '/profile': [],
  '/system-status': ['dashboard.view', 'roles.manage', 'permissions.manage'],
}

export const PROTECTED_MENU_PATHS = [
  '/forums',
  '/products',
  '/messages',
  '/system-status',
]

export const getRequiredPermissions = (path) => ROUTE_ACCESS_RULES[path] ?? null
