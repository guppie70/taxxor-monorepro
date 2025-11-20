<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">


	<xsl:variable name="nonbreaking_space">&#160;</xsl:variable>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>



	<xsl:template match="/">
		<table id="table_uid1610004918567" class="tp-overviewtable">
			<thead data-contenteditable="false">
				<tr>
					<th> </th>
					<th> </th>
					<th class="empty"> </th>
					<th class="ta-l" colspan="5">Financials</th>
					<th class="empty"> </th>
					<th class="ta-l" colspan="6">Total tax contribution</th>
				</tr>
				<tr>
					<th class="col1"> </th>
					<th class="col2">Number of employees</th>
					<th class="col3 empty"> </th>
					<th class="col4">Revenues from third parties</th>
					<th class="col5">Revenues from related parties</th>
					<th class="col6">Profit/Loss before tax</th>
					<th class="col7">Tangible assets</th>
					<th class="col8">Corporate income tax accrued</th>
					<th class="col9 empty"> </th>
					<th class="col10">Corporate income tax paid</th>
					<th class="col13">Customs duties</th>
					<th class="col11">VAT</th>
					<th class="col12">Payroll taxes</th>
					<th class="col14">Other taxes</th>
					<th class="bg_blue col15">Total</th>
				</tr>
			</thead>
			<tbody data-contenteditable="false">
				<!-- Western Europe -->
				<xsl:apply-templates select="overview/geographies/geography[starts-with(@name, 'Western')]"/>

				<!-- North America -->
				<xsl:apply-templates select="overview/geographies/geography[starts-with(@name, 'North')]"/>

				<!-- Other mature geographies -->
				<xsl:apply-templates select="overview/geographies/geography[starts-with(@name, 'Other')]"/>

				<!-- Growth geographies-->
				<xsl:apply-templates select="overview/geographies/geography[starts-with(@name, 'Growth')]">
					<xsl:with-param name="add-empty-row">no</xsl:with-param>
				</xsl:apply-templates>
			</tbody>
		</table>
	</xsl:template>

	<xsl:template match="geography">
		<xsl:param name="add-empty-row">yes</xsl:param>
		<tr class="geography">
			<td>
				<xsl:value-of select="@name"/>
			</td>
			<td> </td>
			<td class="empty"> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
			<td> </td>
		</tr>
		<xsl:for-each select="country">
			<xsl:sort select="@totaltaxpaid" data-type="number" order="descending"/>
			<xsl:sort select="name"/>
			<xsl:apply-templates select="."/>
		</xsl:for-each>

		<xsl:if test="$add-empty-row = 'yes'">
			<tr>
				<td> </td>
				<td> </td>
				<td class="empty"> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
				<td> </td>
			</tr>
		</xsl:if>



	</xsl:template>

	<xsl:template match="country">
		<tr>

			<td>
				<xsl:choose>
					<xsl:when test="name='The Netherlands'">Netherlands</xsl:when>
					<xsl:otherwise><xsl:value-of select="name"/></xsl:otherwise>
				</xsl:choose>
			</td>
			<td>
				<xsl:apply-templates select="span[@label = 'empl']"/>
			</td>
			<td class="empty"> </td>
			<td>
				<xsl:apply-templates select="span[@label = 'rtp']"/>
			</td>
			<td>
				<xsl:apply-templates select="span[@label = 'rrp']"/>
			</td>
			<td>
				<xsl:apply-templates select="span[@label = 'pl']"/>
			</td>
			<td>
				<xsl:apply-templates select="span[@label = 'ta']"/>
			</td>
			<td>
				<xsl:apply-templates select="span[@label = 'it']"/>
			</td>
			<td class="empty"> </td>
			<td>
				<xsl:apply-templates select="span[@label = 'cit']"/>
			</td>
			<td>
				<xsl:apply-templates select="span[@label = 'cd']"/>
			</td>			
			<td>
				<xsl:apply-templates select="span[@label = 'vat']"/>
			</td>
			<td>
				<xsl:apply-templates select="span[@label = 'pt']"/>
			</td>
			<td>
				<xsl:apply-templates select="span[@label = 'ot']"/>
			</td>
			<td class="bg_blue">
				<xsl:value-of select="@totaltaxpaid-formatted"/>
			</td>
		</tr>


	</xsl:template>

	<!-- Add the SDE -->
	<xsl:template match="span">
		<span data-fact-id="{@data-fact-id}">
			<xsl:value-of select="."/>
		</span>
	</xsl:template>

</xsl:stylesheet>
