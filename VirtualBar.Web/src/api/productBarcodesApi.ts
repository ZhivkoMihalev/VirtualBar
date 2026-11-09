import { client } from './client'
import type { PagedResult, ProductBarcode } from '../types'

export const PRODUCT_BARCODES_PAGE_SIZE = 30

/** The barcodes collectors gave catalog products, newest first (admin only, #49). */
export async function getProductBarcodes(search: string, page: number): Promise<PagedResult<ProductBarcode>> {
  const { data } = await client.get<PagedResult<ProductBarcode>>('/product-barcodes', {
    params: { search: search || undefined, page, pageSize: PRODUCT_BARCODES_PAGE_SIZE },
  })
  return data
}

/** Removes a wrong pair, in every form of its barcode (admin only). */
export async function removeProductBarcode(id: string): Promise<void> {
  await client.delete(`/product-barcodes/${id}`)
}
