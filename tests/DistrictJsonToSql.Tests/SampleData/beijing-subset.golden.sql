-- 地域数据插入SQL
-- 生成时间: <ts>

INSERT INTO public.tb_district (guid, code, "name", district_type, parent_code) VALUES
('<uuid>'::uuid, '110000', '北京市', 1, NULL),
('<uuid>'::uuid, '110100', '市辖区', 2, '110000'),
('<uuid>'::uuid, '110101', '东城区', 3, '110100'),
('<uuid>'::uuid, '110101001', '东华门街道', 4, '110101'),
('<uuid>'::uuid, '110101002', '景山街道', 4, '110101'),
('<uuid>'::uuid, '110102', '西城区', 3, '110100'),
('<uuid>'::uuid, '110102001', '西长安街街道', 4, '110102');
